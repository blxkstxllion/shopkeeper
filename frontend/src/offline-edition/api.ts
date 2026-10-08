import { apiClient } from '@/lib/api-client'

export interface LocalStatus {
  setupCompleted: boolean
  storeName: string | null
}

export async function getStatus(): Promise<LocalStatus> {
  const { data } = await apiClient.get<LocalStatus>('/local/status')
  return data
}

export async function completeSetup(storeName: string, currencyCode: string, pin: string): Promise<void> {
  await apiClient.post('/local/setup', { storeName, currencyCode, pin })
}

export async function unlock(pin: string): Promise<boolean> {
  const { data } = await apiClient.post<boolean>('/local/unlock', { pin })
  return data
}

// VITE_API_BASE_URL is "http://127.0.0.1:<port>/api" - /health/live is mapped at the sidecar's
// root, not under /api (same convention ShopKeeper.Api's own Program.cs uses), so this strips
// the suffix the same way lib/format.ts's resolveUploadUrl already does for upload URLs.
const apiOrigin = import.meta.env.VITE_API_BASE_URL.replace(/\/api\/?$/, '')

/** Polls the sidecar's unauthenticated liveness endpoint until it responds or timeoutMs
 * elapses. Needed because Tauri spawning the .NET process (see Phase 2 of the plan) doesn't
 * mean it's ready to accept requests yet - ASP.NET Core startup, EF migrations, and the
 * identity-cache refresh all happen first - and there's no event for "now listening" exposed
 * to the frontend, only this kind of polling. */
export async function waitForBackend(timeoutMs = 15000, intervalMs = 200): Promise<boolean> {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${apiOrigin}/health/live`)
      if (response.ok) return true
    } catch {
      // Connection refused - sidecar isn't listening yet. Keep polling.
    }
    await new Promise((resolve) => setTimeout(resolve, intervalMs))
  }
  return false
}
