import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { getAccessToken, setAccessToken } from './token-store'
import type { ApiErrorPayload } from '@/types/auth'

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

let refreshPromise: Promise<string | null> | null = null

async function refreshAccessToken(): Promise<string | null> {
  try {
    const response = await axios.post(
      `${import.meta.env.VITE_API_BASE_URL}/auth/refresh`,
      {},
      { withCredentials: true },
    )
    const token = response.data.accessToken as string
    setAccessToken(token)
    return token
  } catch {
    setAccessToken(null)
    return null
  }
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

      refreshPromise ??= refreshAccessToken().finally(() => {
        refreshPromise = null
      })

      const newToken = await refreshPromise
      if (newToken) {
        originalRequest.headers.Authorization = `Bearer ${newToken}`
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
