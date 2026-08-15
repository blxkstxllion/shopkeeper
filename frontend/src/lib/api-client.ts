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

let refreshPromise: Promise<AuthResult | null> | null = null

async function refreshAuthSession(): Promise<AuthResult | null> {
  try {
    const response = await axios.post<AuthResult>(
      `${import.meta.env.VITE_API_BASE_URL}/auth/refresh`,
      {},
      { withCredentials: true },
    )
    setAccessToken(response.data.accessToken)
    return response.data
  } catch {
    setAccessToken(null)
    return null
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
export async function dedupedRefresh(): Promise<AuthResult | null> {
  refreshPromise ??= refreshAuthSession().finally(() => {
    refreshPromise = null
  })
  return refreshPromise
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
