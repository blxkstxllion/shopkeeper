export interface YearlySales {
  year: number
  revenue: number
  profit: number
  salesCount: number
}

export interface BusinessAbout {
  businessName: string
  logoUrl: string | null
  description: string | null
  ownerBio: string | null
  salesByYear: YearlySales[]
}

export interface UpdateBusinessAboutPayload {
  description: string | null
  ownerBio: string | null
  logoUrl?: string | null
}
