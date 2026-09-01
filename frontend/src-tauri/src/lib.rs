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
      Ok(())
    })
    .run(tauri::generate_context!())
    .expect("error while running tauri application");
}
