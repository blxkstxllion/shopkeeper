use tauri::Manager;
use tauri_plugin_store::StoreExt;

#[cfg(feature = "offline-sidecar")]
use std::sync::Mutex;
#[cfg(feature = "offline-sidecar")]
use tauri_plugin_shell::{process::CommandChild, process::CommandEvent, ShellExt};

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
  let builder = tauri::Builder::default()
    // Backs the session cache (see frontend/src/lib/session-cache.ts) with a real file Tauri
    // writes directly, instead of the webview's own localStorage - needed because on this
    // app's actual WebView2 runtime, localStorage writes were observed to not reliably survive
    // an app restart (confirmed correct in a normal browser via the same code, so this is a
    // storage-backend problem, not an application logic one). Registered unconditionally,
    // unlike the debug-only log plugin below - session persistence must work in release builds.
    .plugin(tauri_plugin_store::Builder::default().build());

  #[cfg(feature = "offline-sidecar")]
  let builder = builder.plugin(tauri_plugin_shell::init());

  let builder = builder.setup(|app| {
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

      // Offline Edition only: spawn the bundled .NET backend (see backend/src/ShopKeeper.Api.Local)
      // as a sidecar process. The handle is stashed in managed state so RunEvent::ExitRequested
      // below can explicitly kill it - confirmed by hands-on testing that Tauri does NOT auto-
      // terminate a spawned sidecar when the window closes on Windows; left running, it would
      // keep the SQLite file locked for the next launch.
      #[cfg(feature = "offline-sidecar")]
      {
        let (mut rx, child) = app.shell().sidecar("shopkeeper-server")?.spawn()?;
        app.manage(Mutex::new(Some(child)));

        tauri::async_runtime::spawn(async move {
          while let Some(event) = rx.recv().await {
            match event {
              CommandEvent::Stdout(line) => log::info!("sidecar: {}", String::from_utf8_lossy(&line)),
              CommandEvent::Stderr(line) => log::error!("sidecar: {}", String::from_utf8_lossy(&line)),
              _ => {}
            }
          }
        });
      }

      Ok(())
    });

  #[cfg(feature = "offline-sidecar")]
  {
    builder
      .build(tauri::generate_context!())
      .expect("error while building tauri application")
      .run(|app_handle, event| {
        if let tauri::RunEvent::ExitRequested { .. } = event {
          if let Some(state) = app_handle.try_state::<Mutex<Option<CommandChild>>>() {
            if let Some(child) = state.lock().unwrap().take() {
              let _ = child.kill();
            }
          }
        }
      });
  }

  #[cfg(not(feature = "offline-sidecar"))]
  {
    builder
      .run(tauri::generate_context!())
      .expect("error while running tauri application");
  }
}
