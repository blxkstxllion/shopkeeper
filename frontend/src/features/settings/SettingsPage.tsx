import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Store, Percent, ShieldCheck, Users, Bell, Plug, CreditCard, Webhook, type LucideIcon } from 'lucide-react'
import { EmptyState } from '@/components/ui/EmptyState'
import { BusinessProfileSection } from './BusinessProfileSection'
import { TaxSettingsSection } from './TaxSettingsSection'
import { TwoFactorSection } from './TwoFactorSection'
import { SessionsSection } from './SessionsSection'
import { PlanBillingSection } from './PlanBillingSection'
import { RolesSection } from './RolesSection'
import { NotificationPreferencesSection } from './NotificationPreferencesSection'

type SectionId = 'business' | 'tax' | 'roles' | 'notifications' | 'security' | 'integrations' | 'subscription' | 'api'

interface SectionConfig {
  id: SectionId
  label: string
  icon: LucideIcon
  comingSoonPhase?: string
}

const SECTIONS: SectionConfig[] = [
  { id: 'business', label: 'Business', icon: Store },
  { id: 'tax', label: 'Tax & currency', icon: Percent },
  { id: 'roles', label: 'Roles & permissions', icon: Users },
  { id: 'notifications', label: 'Notifications', icon: Bell },
  { id: 'security', label: 'Security', icon: ShieldCheck },
  { id: 'integrations', label: 'Integrations', icon: Plug, comingSoonPhase: 'Phase 7' },
  { id: 'subscription', label: 'Subscription', icon: CreditCard },
  { id: 'api', label: 'API & webhooks', icon: Webhook, comingSoonPhase: 'Phase 7' },
]

export function SettingsPage() {
  const [searchParams] = useSearchParams()
  const requestedSection = searchParams.get('section')
  const initialSection = SECTIONS.some((s) => s.id === requestedSection) ? (requestedSection as SectionId) : 'business'
  const [activeId, setActiveId] = useState<SectionId>(initialSection)
  const active = SECTIONS.find((s) => s.id === activeId)!

  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-6">
        <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Settings</h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Manage your business, security, and account preferences.
        </p>
      </div>

      <div className="flex flex-col gap-6 md:flex-row">
        <nav className="flex shrink-0 gap-1 overflow-x-auto md:w-52 md:flex-col md:overflow-visible">
          {SECTIONS.map((section) => (
            <button
              key={section.id}
              type="button"
              onClick={() => setActiveId(section.id)}
              className={`flex shrink-0 items-center gap-2 rounded-lg px-3 py-2 text-left text-sm font-medium transition-colors ${
                section.id === activeId
                  ? 'bg-primary-50 text-primary-700 dark:bg-primary-900/30 dark:text-primary-300'
                  : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800'
              }`}
            >
              <section.icon className="h-4 w-4 shrink-0" />
              {section.label}
            </button>
          ))}
        </nav>

        <div className="min-w-0 flex-1">
          {active.comingSoonPhase ? (
            <EmptyState
              icon={active.icon}
              title={`${active.label} is coming in ${active.comingSoonPhase}`}
              description="This area of The Shop Keeper hasn't been built yet. Check back as development continues through the phases."
            />
          ) : activeId === 'business' ? (
            <BusinessProfileSection />
          ) : activeId === 'tax' ? (
            <TaxSettingsSection />
          ) : activeId === 'security' ? (
            <div className="flex flex-col gap-4">
              <TwoFactorSection />
              <SessionsSection />
            </div>
          ) : activeId === 'subscription' ? (
            <PlanBillingSection />
          ) : activeId === 'roles' ? (
            <RolesSection />
          ) : activeId === 'notifications' ? (
            <NotificationPreferencesSection />
          ) : null}
        </div>
      </div>
    </div>
  )
}
