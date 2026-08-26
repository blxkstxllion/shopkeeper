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

export const goalOptions: { value: string; label: string }[] = [
  { value: 'IncreaseProfit', label: 'Increase profit' },
  { value: 'ReduceStockLosses', label: 'Reduce stock losses' },
  { value: 'ImproveInventory', label: 'Improve inventory' },
  { value: 'ManageBranches', label: 'Manage branches' },
  { value: 'TrackExpenses', label: 'Track expenses' },
  { value: 'ImproveEmployeePerformance', label: 'Improve employee performance' },
]

export const onboardingSchema = z.object({
  businessName: z.string().min(1, 'Business name is required').max(200),
  businessType: z.enum(businessTypes),
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

export type OnboardingFormValues = z.infer<typeof onboardingSchema>

export const onboardingDefaults: OnboardingFormValues = {
  businessName: '',
  businessType: 'Retail',
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
  0: ['businessName', 'businessType', 'country', 'currencyCode'],
  1: ['firstBranchName', 'firstBranchAddress', 'firstBranchCity'],
  2: ['taxEnabled', 'taxRatePercent', 'taxInclusivePricing'],
  3: ['goals'],
}
