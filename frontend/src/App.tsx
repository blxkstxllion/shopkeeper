import { BrowserRouter } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { queryClient } from '@/lib/query-client'
import { AuthProvider } from '@/contexts/AuthContext'
import { ThemeProvider } from '@/contexts/ThemeContext'
import { AppRouter } from '@/routes/AppRouter'
import { UpdateAvailableBanner } from '@/offline/UpdateAvailableBanner'
import { OfflineSyncProvider } from '@/offline/OfflineSyncContext'

export default function App() {
  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <AuthProvider>
            <OfflineSyncProvider>
              <AppRouter />
            </OfflineSyncProvider>
          </AuthProvider>
        </BrowserRouter>
        <UpdateAvailableBanner />
        {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
      </QueryClientProvider>
    </ThemeProvider>
  )
}
