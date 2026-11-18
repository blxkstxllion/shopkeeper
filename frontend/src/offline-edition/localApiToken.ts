import { invoke } from '@tauri-apps/api/core'

let cachedToken: string | null = null

/** Retrieves the per-launch random token the Rust shell captured from the sidecar's own stdout
 * (see src-tauri/src/lib.rs's get_local_api_token command, and LocalApiTokenMiddleware on the
 * .NET side - that middleware is the sidecar's real authorization boundary, since its CORS
 * policy is wide-open and LocalAuthenticationHandler authenticates every request as the owner
 * unconditionally). Cached after the first successful fetch since the token never changes for
 * this process's lifetime. Retries briefly: the sidecar prints the token within microseconds of
 * its own startup, long before it's listening for HTTP connections, but the two aren't formally
 * ordered from this side. */
export async function getLocalApiToken(): Promise<string | null> {
  if (cachedToken) return cachedToken

  for (let attempt = 0; attempt < 50; attempt++) {
    const token = await invoke<string | null>('get_local_api_token')
    if (token) {
      cachedToken = token
      return token
    }
    await new Promise((resolve) => setTimeout(resolve, 100))
  }

  return null
}
