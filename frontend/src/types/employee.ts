export interface Role {
  id: string
  name: string
}

export interface BusinessMember {
  businessUserId: string
  userId: string
  firstName: string
  lastName: string
  email: string
  roleName: string
  branchName: string | null
  status: 'Invited' | 'Active' | 'Suspended' | 'Removed'
  isOwner: boolean
  joinedAt: string | null
}

export interface PendingInvitationItem {
  id: string
  email: string
  roleName: string
  branchName: string | null
  invitedAt: string
  expiresAt: string
}

export interface BusinessUsersResponse {
  members: BusinessMember[]
  pendingInvitations: PendingInvitationItem[]
}

export interface InvitationDetails {
  businessId: string
  email: string
  businessName: string
  inviterName: string
  roleName: string
  userAlreadyExists: boolean
}

export interface InviteEmployeePayload {
  email: string
  roleId: string
  branchId?: string | null
}

export interface AcceptInvitationPayload {
  password: string
  firstName: string
  lastName: string
}
