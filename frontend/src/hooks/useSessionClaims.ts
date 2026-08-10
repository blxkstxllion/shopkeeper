import { useEffect, useState } from 'react'
import { getAccessToken, onAccessTokenChange } from '@/lib/token-store'
import { decodeSessionClaims, type SessionClaims } from '@/lib/jwt'

export function useSessionClaims(): SessionClaims | null {
  const [claims, setClaims] = useState<SessionClaims | null>(() => decodeSessionClaims(getAccessToken()))

  useEffect(() => onAccessTokenChange((token) => setClaims(decodeSessionClaims(token))), [])

  return claims
}
