export interface UserBusiness {
  businessId: string
  businessName: string
  roleName: string
  isOwner: boolean
  onboardingCompleted: boolean
}

export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  isEmailVerified: boolean
  businesses: UserBusiness[]
}

export interface AuthResult {
  accessToken: string
  accessTokenExpiresAt: string
  user: User
}

export interface ApiErrorPayload {
  title: string
  status: number
  errors?: Record<string, string[]> | null
}
