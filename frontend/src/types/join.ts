export interface JoinBusinessInfo {
  businessId: string
  businessName: string
}

export interface SubmitJoinRequestPayload {
  firstName: string
  lastName: string
  email: string
  phone: string
  password: string
}
