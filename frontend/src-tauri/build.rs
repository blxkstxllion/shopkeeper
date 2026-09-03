use std::collections::hash_map::DefaultHasher;
use std::hash::{Hash, Hasher};

fn main() {
  // index.html references the current build's content-hashed asset filenames (e.g.
  // assets/index-XXXXX.js), so hashing it is a cheap proxy for "did the frontend build
  // actually change" - see lib.rs, which compares this against a stored marker on startup
  // to detect a stale WebView2 HTTP cache surviving across an app update.
  let dist_index = std::fs::read_to_string("../dist/index.html").unwrap_or_default();
  let mut hasher = DefaultHasher::new();
  dist_index.hash(&mut hasher);
  println!("cargo:rustc-env=FRONTEND_ASSET_HASH={:x}", hasher.finish());
  println!("cargo:rerun-if-changed=../dist/index.html");

  tauri_build::build()
}
