import type { LucideIcon } from 'lucide-react'
import { Construction } from 'lucide-react'
import { EmptyState } from '@/components/ui/EmptyState'

/**
 * Placeholder for nav destinations not yet built. Per the product spec, unfinished
 * areas must say so plainly rather than showing fabricated data or dead buttons.
 */
export function ComingSoon({ title, icon: Icon = Construction, phase }: { title: string; icon?: LucideIcon; phase: string }) {
  return (
    <div className="mx-auto max-w-lg pt-12">
      <EmptyState icon={Icon} title={`${title} is coming in ${phase}`} description="This area of The Shop Keeper hasn't been built yet. Check back as development continues through the phases." />
    </div>
  )
}
