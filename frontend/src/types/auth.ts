export interface UserBusiness {
  businessId: string
  businessName: string
  roleName: string
  isOwner: boolean
  onboardingCompleted: boolean
  currencyCode: string
  colorTheme: string
}

export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  isEmailVerified: boolean
  /** True only for accounts created after verification enforcement shipped and still
   * unverified - use this (not isEmailVerified alone) to decide whether to block the app,
   * since existing pre-enforcement accounts stay usable regardless of verification status. */
  mustVerifyEmail: boolean
  photoUrl: string | null
  businesses: UserBusiness[]
}

export interface AuthResult {
  accessToken: string
  accessTokenExpiresAt: string
  user: User
}

export type LoginResponse =
  { requiresTwoFactor: true; challengeToken: string } | { requiresTwoFactor: false; auth: AuthResult }

export interface Session {
  id: string
  createdAt: string
  expiresAt: string
  createdByIp: string | null
  userAgent: string | null
  isCurrent: boolean
}

export interface TwoFactorStatus {
  enabled: boolean
}

export interface TwoFactorSetup {
  secret: string
  provisioningUri: string
}

export interface ApiErrorPayload {
  title: string
  status: number
  errors?: Record<string, string[]> | null
}
