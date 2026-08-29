import { z } from 'zod'

export const businessTypes = [
  'Retail',
  'Restaurant',
  'Grocery',
  'Pharmacy',
  'Electronics',
  'Fashion',
  'Wholesale',
  'Services',
  'Other',
] as const

// Suggested brand color per business type, offered (not forced) as a prompt when the user picks
// a type - researched real color-convention precedent (retail pharmacy chains, food/appetite
// branding, general retail/tech trust) rather than guessed. Types not listed here, or mapped to
// 'green', get no suggestion prompt since 'green' is already the baseline default.
export const suggestedColorThemeByBusinessType: Partial<Record<(typeof businessTypes)[number], 'blue' | 'red'>> = {
  Pharmacy: 'red',
  Restaurant: 'blue',
  Retail: 'blue',
  Electronics: 'blue',
  Fashion: 'blue',
  Wholesale: 'blue',
  Services: 'blue',
}

// Weighted toward Africa (this app's primary market) with the other major markets a business
// might realistically operate from. Free-text was the alternative but let currencyCode drift
// into values Intl.NumberFormat can't format (see formatMoney's catch) - a fixed list of real
// ISO 4217 codes prevents that at the source instead of failing silently later.
export const countries: { value: string; label: string }[] = [
  { value: 'Ghana', label: 'Ghana' },
  { value: 'Nigeria', label: 'Nigeria' },
  { value: 'Kenya', label: 'Kenya' },
  { value: 'South Africa', label: 'South Africa' },
  { value: 'Uganda', label: 'Uganda' },
  { value: 'Tanzania', label: 'Tanzania' },
  { value: 'Rwanda', label: 'Rwanda' },
  { value: 'Zambia', label: 'Zambia' },
  { value: 'Malawi', label: 'Malawi' },
  { value: 'Botswana', label: 'Botswana' },
  { value: 'Zimbabwe', label: 'Zimbabwe' },
  { value: 'Namibia', label: 'Namibia' },
  { value: 'Mozambique', label: 'Mozambique' },
  { value: 'Ethiopia', label: 'Ethiopia' },
  { value: 'Egypt', label: 'Egypt' },
  { value: 'Morocco', label: 'Morocco' },
  { value: 'Algeria', label: 'Algeria' },
  { value: 'Tunisia', label: 'Tunisia' },
  { value: 'Senegal', label: 'Senegal' },
  { value: 'Ivory Coast', label: 'Ivory Coast' },
  { value: 'Cameroon', label: 'Cameroon' },
  { value: 'DR Congo', label: 'DR Congo' },
  { value: 'Republic of Congo', label: 'Republic of Congo' },
  { value: 'Mali', label: 'Mali' },
  { value: 'Burkina Faso', label: 'Burkina Faso' },
  { value: 'Sierra Leone', label: 'Sierra Leone' },
  { value: 'Liberia', label: 'Liberia' },
  { value: 'Angola', label: 'Angola' },
  { value: 'Mauritius', label: 'Mauritius' },
  { value: 'United Kingdom', label: 'United Kingdom' },
  { value: 'United States', label: 'United States' },
  { value: 'Canada', label: 'Canada' },
  { value: 'France', label: 'France' },
  { value: 'Germany', label: 'Germany' },
  { value: 'United Arab Emirates', label: 'United Arab Emirates' },
  { value: 'India', label: 'India' },
  { value: 'China', label: 'China' },
  { value: 'Australia', label: 'Australia' },
] as const

export const currencies: { value: string; label: string }[] = [
  { value: 'GHS', label: 'GHS — Ghanaian Cedi' },
  { value: 'NGN', label: 'NGN — Nigerian Naira' },
  { value: 'KES', label: 'KES — Kenyan Shilling' },
  { value: 'ZAR', label: 'ZAR — South African Rand' },
  { value: 'UGX', label: 'UGX — Ugandan Shilling' },
  { value: 'TZS', label: 'TZS — Tanzanian Shilling' },
  { value: 'RWF', label: 'RWF — Rwandan Franc' },
  { value: 'ZMW', label: 'ZMW — Zambian Kwacha' },
  { value: 'MWK', label: 'MWK — Malawian Kwacha' },
  { value: 'BWP', label: 'BWP — Botswana Pula' },
  { value: 'NAD', label: 'NAD — Namibian Dollar' },
  { value: 'MZN', label: 'MZN — Mozambican Metical' },
  { value: 'ETB', label: 'ETB — Ethiopian Birr' },
  { value: 'EGP', label: 'EGP — Egyptian Pound' },
  { value: 'MAD', label: 'MAD — Moroccan Dirham' },
  { value: 'XOF', label: 'XOF — West African CFA Franc' },
  { value: 'XAF', label: 'XAF — Central African CFA Franc' },
  { value: 'CDF', label: 'CDF — Congolese Franc' },
  { value: 'SLL', label: 'SLL — Sierra Leonean Leone' },
  { value: 'LRD', label: 'LRD — Liberian Dollar' },
  { value: 'AOA', label: 'AOA — Angolan Kwanza' },
  { value: 'MUR', label: 'MUR — Mauritian Rupee' },
  { value: 'GBP', label: 'GBP — British Pound' },
  { value: 'USD', label: 'USD — US Dollar' },
  { value: 'EUR', label: 'EUR — Euro' },
  { value: 'CAD', label: 'CAD — Canadian Dollar' },
  { value: 'AED', label: 'AED — UAE Dirham' },
  { value: 'INR', label: 'INR — Indian Rupee' },
  { value: 'CNY', label: 'CNY — Chinese Yuan' },
  { value: 'AUD', label: 'AUD — Australian Dollar' },
] as const

export const goalOptions: { value: string; label: string }[] = [
  { value: 'IncreaseProfit', label: 'Increase profit' },
  { value: 'ReduceStockLosses', label: 'Reduce stock losses' },
  { value: 'ImproveInventory', label: 'Improve inventory' },
  { value: 'ManageBranches', label: 'Manage branches' },
  { value: 'TrackExpenses', label: 'Track expenses' },
  { value: 'ImproveEmployeePerformance', label: 'Improve employee performance' },
]

export const onboardingSchema = z
  .object({
    businessName: z.string().min(1, 'Business name is required').max(200),
    businessType: z.enum(businessTypes),
    businessTypeOther: z.string().max(200).optional(),
    country: z.string().min(1, 'Country is required'),
    currencyCode: z
      .string()
      .min(3, 'Use a 3-letter currency code')
      .max(3, 'Use a 3-letter currency code')
      .transform((v) => v.toUpperCase()),
    taxEnabled: z.boolean(),
    taxRatePercent: z.number().min(0, 'Must be 0 or more').max(100, 'Must be 100 or less'),
    taxInclusivePricing: z.boolean(),
    goals: z.array(z.string()).min(1, 'Pick at least one goal'),
    firstBranchName: z.string().min(1, 'Branch name is required').max(200),
    firstBranchAddress: z.string().optional(),
    firstBranchCity: z.string().optional(),
    colorTheme: z.enum(['blue', 'red', 'green']),
  })
  .superRefine((data, ctx) => {
    if (data.businessType === 'Other' && !data.businessTypeOther?.trim()) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Tell us what kind of business this is',
        path: ['businessTypeOther'],
      })
    }
  })

export type OnboardingFormValues = z.infer<typeof onboardingSchema>

export const onboardingDefaults: OnboardingFormValues = {
  businessName: '',
  businessType: 'Retail',
  businessTypeOther: '',
  country: 'Ghana',
  currencyCode: 'GHS',
  taxEnabled: false,
  taxRatePercent: 0,
  taxInclusivePricing: true,
  goals: [],
  firstBranchName: '',
  firstBranchAddress: '',
  firstBranchCity: '',
  colorTheme: 'green',
}

export const stepFields: Record<number, (keyof OnboardingFormValues)[]> = {
  0: ['businessName', 'businessType', 'businessTypeOther', 'country', 'currencyCode'],
  1: ['firstBranchName', 'firstBranchAddress', 'firstBranchCity'],
  2: ['taxEnabled', 'taxRatePercent', 'taxInclusivePricing'],
  3: ['goals'],
}
