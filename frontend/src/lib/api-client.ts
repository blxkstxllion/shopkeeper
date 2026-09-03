import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { getAccessToken, setAccessToken } from './token-store'
import type { ApiErrorPayload, AuthResult } from '@/types/auth'

export class ApiError extends Error {
  status: number
  fieldErrors?: Record<string, string[]> | null

  constructor(payload: ApiErrorPayload) {
    super(payload.title)
    this.name = 'ApiError'
    this.status = payload.status
    this.fieldErrors = payload.errors
  }
}

/** True only when the request never reached the server (offline, DNS failure, the API
 * being down) - see the response interceptor below, which stamps status 0 exactly in
 * that case. A real 4xx/5xx means the server was reached and rejected the request for
 * an actual reason, which callers must never treat the same as "try again later". */
export function isNetworkError(err: unknown): boolean {
  return err instanceof ApiError && err.status === 0
}

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  withCredentials: true, // send the httpOnly refresh-token cookie
})

apiClient.interceptors.request.use((config) => {
  const token = getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

export type SessionCheckOutcome =
  | { kind: 'authenticated'; result: AuthResult }
  | { kind: 'unauthenticated' } // the server actually said no - refresh token missing/expired/revoked
  | { kind: 'network-error' } // couldn't reach the server at all - offline, DNS failure, timeout

let refreshPromise: Promise<SessionCheckOutcome> | null = null

async function refreshAuthSession(): Promise<SessionCheckOutcome> {
  try {
    const response = await axios.post<AuthResult>(
      `${import.meta.env.VITE_API_BASE_URL}/auth/refresh`,
      {},
      { withCredentials: true, timeout: 8000 },
    )
    setAccessToken(response.data.accessToken)
    return { kind: 'authenticated', result: response.data }
  } catch (err) {
    // No response at all (offline/timeout/DNS/CORS) means we genuinely don't know whether
    // the session is still valid - don't clear the access token or force a logout on what
    // might just be a cold start with no network yet (see AuthContext's mount effect).
    if (!(err as AxiosError).response) {
      return { kind: 'network-error' }
    }
    setAccessToken(null)
    return { kind: 'unauthenticated' }
  }
}

/**
 * The single entry point for refreshing a session - both the 401-retry interceptor
 * below and AuthContext's mount-time session check call this, sharing one in-flight
 * request. Necessary because refresh tokens rotate on use: two independent, un-deduped
 * calls (e.g. React StrictMode double-invoking an effect) would race to redeem the same
 * token, and the second one hitting an already-rotated token trips the API's token-theft
 * detection, revoking the whole session and logging the user straight back out.
 */
function dedupedRefreshOutcome(): Promise<SessionCheckOutcome> {
  refreshPromise ??= refreshAuthSession().finally(() => {
    refreshPromise = null
  })
  return refreshPromise
}

/** Used by the 401-retry interceptor below, which only cares whether the retry can proceed. */
export async function dedupedRefresh(): Promise<AuthResult | null> {
  const outcome = await dedupedRefreshOutcome()
  return outcome.kind === 'authenticated' ? outcome.result : null
}

/** Used once at app boot (AuthContext), which needs to tell a real "you're logged out" apart
 * from "couldn't check, we're offline" - a native app cold-starting with no network yet must
 * not be treated the same as an actually-expired session. */
export function checkInitialSession(): Promise<SessionCheckOutcome> {
  return dedupedRefreshOutcome()
}

interface RetryableConfig extends InternalAxiosRequestConfig {
  _retried?: boolean
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiErrorPayload>) => {
    const originalRequest = error.config as RetryableConfig | undefined
    const isRefreshCall = originalRequest?.url?.includes('/auth/refresh')

    if (error.response?.status === 401 && originalRequest && !originalRequest._retried && !isRefreshCall) {
      originalRequest._retried = true

      const result = await dedupedRefresh()
      if (result) {
        originalRequest.headers.Authorization = `Bearer ${result.accessToken}`
        return apiClient(originalRequest)
      }
    }

    if (error.response?.data?.title) {
      return Promise.reject(new ApiError(error.response.data))
    }

    return Promise.reject(
      new ApiError({
        title: 'Unable to reach the server. Please check your connection and try again.',
        status: error.response?.status ?? 0,
      }),
    )
  },
)
