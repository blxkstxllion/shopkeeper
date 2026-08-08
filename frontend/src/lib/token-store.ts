let accessToken: string | null = null
const listeners = new Set<(token: string | null) => void>()

export function getAccessToken() {
  return accessToken
}

export function setAccessToken(token: string | null) {
  accessToken = token
  listeners.forEach((listener) => listener(token))
}

/** Lets AuthContext react when the API client clears the token after a failed silent refresh. */
export function onAccessTokenChange(listener: (token: string | null) => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}
