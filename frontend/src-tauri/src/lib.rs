use tauri::Manager;
use tauri_plugin_store::StoreExt;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
  tauri::Builder::default()
    // Backs the session cache (see frontend/src/lib/session-cache.ts) with a real file Tauri
    // writes directly, instead of the webview's own localStorage - needed because on this
    // app's actual WebView2 runtime, localStorage writes were observed to not reliably survive
    // an app restart (confirmed correct in a normal browser via the same code, so this is a
    // storage-backend problem, not an application logic one). Registered unconditionally,
    // unlike the debug-only log plugin below - session persistence must work in release builds.
    .plugin(tauri_plugin_store::Builder::default().build())
    .setup(|app| {
      if cfg!(debug_assertions) {
        app.handle().plugin(
          tauri_plugin_log::Builder::default()
            .level(log::LevelFilter::Info)
            .build(),
        )?;
      }

      // WebView2 was observed to keep serving a cached copy of index.html (and the hashed
      // JS/CSS it references) across app updates - the browser-level HTTP cache lives in the
      // WebView2 profile, which survives a reinstall, and index.html itself has no
      // content-hashed filename to naturally bust that cache the way its own asset references
      // do. FRONTEND_ASSET_HASH (see build.rs) changes whenever the frontend build actually
      // changes; comparing it against what was last seen lets a real update force a one-time
      // cache clear + reload instead of silently continuing to run stale code forever. This
      // does also clear cookies and the offline product/customer catalog (IndexedDB) - no more
      // granular WebView2 API is exposed through Tauri. The cached login session itself
      // (session-cache.ts) survives, since it's backed by this same store plugin rather than
      // WebView2 storage, but losing the refresh-token cookie means the next background auth
      // check gets a real 401 and signs the user out anyway - one extra sign-in per real update,
      // a small, deliberate trade-off against an update that silently never takes effect.
      let current_hash = env!("FRONTEND_ASSET_HASH");
      let cache_store = app.store("cache-version.json")?;
      let stored_hash = cache_store.get("frontend_asset_hash").and_then(|v| v.as_str().map(str::to_string));

      if stored_hash.as_deref() != Some(current_hash) {
        if let Some(window) = app.get_webview_window("main") {
          let _ = window.clear_all_browsing_data();
          let _ = window.eval("location.reload()");
        }
        cache_store.set("frontend_asset_hash", current_hash);
        cache_store.save()?;
      }

      Ok(())
    })
    .run(tauri::generate_context!())
    .expect("error while running tauri application");
}
