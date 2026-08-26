// Mirrors format.ts's setActiveCurrencyCode - business-level branding, not a personal device
// preference like light/dark (ThemeContext), so it's applied via a plain DOM attribute driven
// by AuthContext rather than its own React context or localStorage.
export function applyColorTheme(colorTheme: string): void {
  document.documentElement.setAttribute('data-color-theme', colorTheme)
}
