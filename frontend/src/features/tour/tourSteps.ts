export interface TourStep {
  id: string
  /** CSS selector for the element to spotlight, or null for a centered, targetless step. */
  target: string | null
  title: string
  body: string
}

export const tourSteps: TourStep[] = [
  {
    id: 'welcome',
    target: null,
    title: 'Welcome to The Shop Keeper 👋',
    body: "Let's take a 30-second look at where everything lives. You can skip this anytime.",
  },
  {
    id: 'dashboard',
    target: '[data-tour="nav-dashboard"]',
    title: 'Your dashboard',
    body: "Today's sales, profit, and low-stock alerts, all in one place.",
  },
  {
    id: 'sell',
    target: '[data-tour="nav-sell"]',
    title: 'Make a sale',
    body: 'This is where you ring up sales, take payments, and print receipts.',
  },
  {
    id: 'inventory',
    target: '[data-tour="nav-inventory"]',
    title: 'Manage inventory',
    body: 'Add products, track stock levels, and get warned before you run out.',
  },
  {
    id: 'reports',
    target: '[data-tour="nav-reports"]',
    title: 'Reports',
    body: 'Dig into profit, expenses, and inventory trends whenever you need to.',
  },
  {
    id: 'ai',
    target: '[data-tour="nav-ai"]',
    title: 'AI Advisor',
    body: 'Ask questions about your business in plain language and get real, grounded answers.',
  },
  {
    id: 'done',
    target: null,
    title: "You're all set!",
    body: 'You can restart this tour anytime from the account menu in the top right.',
  },
]
