export interface SessionClaims {
  userId: string
  email: string
  businessId: string | null
  /** Null means unrestricted (Owner/Administrator) - can act on any branch in the business. */
  branchId: string | null
  isOwner: boolean
  permissions: string[]
}

/**
 * Reads claims out of the current access token for UI decisions only (e.g. hide the branch
 * switcher for a Cashier). This is NOT a trust boundary - the signature is never checked here,
 * and every write the UI makes is independently re-authorized by the backend regardless of
 * what this returns.
 */
export function decodeSessionClaims(token: string | null): SessionClaims | null {
  if (!token) return null

  try {
    const payload = token.split('.')[1]
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'))
    const claims = JSON.parse(json)

    const permissions = claims['permission']
    return {
      userId: claims['sub'],
      email: claims['email'],
      businessId: claims['business_id'] ?? null,
      branchId: claims['branch_id'] ?? null,
      isOwner: claims['is_owner'] === 'true',
      permissions: Array.isArray(permissions) ? permissions : permissions ? [permissions] : [],
    }
  } catch {
    return null
  }
}
