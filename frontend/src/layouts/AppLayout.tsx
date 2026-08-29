import { Outlet } from 'react-router-dom'
import { BranchProvider } from '@/contexts/BranchContext'
import { TourProvider } from '@/features/tour/TourContext'
import { TourOverlay } from '@/features/tour/TourOverlay'
import { Sidebar } from './Sidebar'
import { TopNav } from './TopNav'
import { MobileBottomNav } from './MobileBottomNav'
import { EmailVerificationBanner } from './EmailVerificationBanner'

export function AppLayout() {
  return (
    <BranchProvider>
      <TourProvider>
        <div className="flex h-screen overflow-hidden bg-slate-50 dark:bg-slate-950">
          <Sidebar />
          <div className="flex min-w-0 flex-1 flex-col">
            <TopNav />
            <EmailVerificationBanner />
            <main className="flex-1 overflow-y-auto p-4 pb-20 lg:p-6 lg:pb-6">
              <Outlet />
            </main>
          </div>
          <MobileBottomNav />
        </div>
        <TourOverlay />
      </TourProvider>
    </BranchProvider>
  )
}
