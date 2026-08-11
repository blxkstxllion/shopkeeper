export interface BusinessSettings {
  businessId: string
  name: string
  legalName: string | null
  businessType: string
  country: string
  currencyCode: string
  timeZone: string
  logoUrl: string | null
  taxEnabled: boolean
  taxIdNumber: string | null
  taxRatePercent: number
  taxInclusivePricing: boolean
}

export interface UpdateBusinessProfilePayload {
  name: string
  legalName?: string | null
  timeZone: string
}

export interface UpdateTaxSettingsPayload {
  taxEnabled: boolean
  taxIdNumber?: string | null
  taxRatePercent: number
  taxInclusivePricing: boolean
}
