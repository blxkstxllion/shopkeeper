import { load as loadTauriStore, type Store } from '@tauri-apps/plugin-store'
import type { User } from '@/types/auth'

const STORAGE_KEY = 'shopkeeper.session-snapshot'
const IS_TAURI = import.meta.env.MODE === 'tauri'

interface SessionSnapshot {
  user: User
  activeBusinessId: string | null
}

// Statically imported above (not a runtime `import('@tauri-apps/plugin-store')`) - a dynamic
// import gets code-split into a separate chunk that Vite fetches at runtime, and that fetch was
// observed to silently fail specifically when served through Tauri's production asset-protocol
// origin (http://tauri.localhost), even though the exact same dynamic import worked fine over a
// real HTTP dev server. A static import is resolved at build time and bundled inline instead,
// so there's no separate chunk to fail to load. Safe to import unconditionally: merely importing
// this module doesn't touch Tauri's IPC bridge, only calling its functions does (guarded by
// IS_TAURI below), so this doesn't break the web/PWA build that ships this same file.
let tauriStorePromise: Promise<Store> | null = null
function getTauriStore() {
  tauriStorePromise ??= loadTauriStore('session-cache.json')
  return tauriStorePromise
}

/**
 * The access token itself is deliberately never persisted (in-memory only, see
 * token-store.ts) - this cache holds just enough profile/business metadata, already
 * visible to the logged-in user anyway, to render the app shell on a cold start that
 * can't reach the server to refresh. It is not a credential and grants no access on its
 * own; every subsequent API call still needs a real access token to succeed.
 *
 * Backed by tauri-plugin-store (a real file Tauri writes directly - see src-tauri/src/lib.rs)
 * in the desktop build, and by localStorage everywhere else. Not a stylistic choice: on this
 * app's actual WebView2 runtime, localStorage writes were observed to not reliably survive an
 * app restart, even though the exact same save/load code worked correctly in a normal browser -
 * a storage-backend problem specific to that environment, not an application logic one.
 */
export async function saveSessionSnapshot(user: User, activeBusinessId: string | null): Promise<void> {
  const snapshot: SessionSnapshot = { user, activeBusinessId }
  try {
    if (IS_TAURI) {
      const store = await getTauriStore()
      await store.set(STORAGE_KEY, snapshot)
      await store.save()
    } else {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot))
    }
  } catch (err) {
    console.error('[session-cache] saveSessionSnapshot failed', err)
  }
}

export async function loadSessionSnapshot(): Promise<SessionSnapshot | null> {
  try {
    if (IS_TAURI) {
      const store = await getTauriStore()
      return (await store.get<SessionSnapshot>(STORAGE_KEY)) ?? null
    }
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as SessionSnapshot) : null
  } catch (err) {
    console.error('[session-cache] loadSessionSnapshot failed', err)
    return null
  }
}

export async function clearSessionSnapshot(): Promise<void> {
  try {
    if (IS_TAURI) {
      const store = await getTauriStore()
      await store.delete(STORAGE_KEY)
      await store.save()
    } else {
      localStorage.removeItem(STORAGE_KEY)
    }
  } catch {
    // Nothing to do - see saveSessionSnapshot.
  }
}
