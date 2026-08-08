import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Building2, ChevronRight } from 'lucide-react'
import { useAuth } from '@/contexts/AuthContext'
import { Card } from '@/components/ui/Card'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'

export function SelectBusinessPage() {
  const { user, selectBusiness } = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const [pendingId, setPendingId] = useState<string | null>(null)

  if (!user) return null

  const handleSelect = async (businessId: string) => {
    setError(null)
    setPendingId(businessId)
    try {
      await selectBusiness(businessId)
      navigate('/app', { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to switch businesses. Please try again.')
    } finally {
      setPendingId(null)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-12 dark:bg-slate-950">
      <div className="w-full max-w-md">
        <h1 className="mb-1 text-center text-lg font-semibold text-slate-900 dark:text-slate-100">Choose a business</h1>
        <p className="mb-6 text-center text-sm text-slate-500 dark:text-slate-400">
          You have access to multiple businesses. Select one to continue.
        </p>

        {error && (
          <div className="mb-4">
            <Alert tone="error">{error}</Alert>
          </div>
        )}

        <Card className="divide-y divide-slate-100 dark:divide-slate-800">
          {user.businesses.map((b) => (
            <button
              key={b.businessId}
              onClick={() => handleSelect(b.businessId)}
              disabled={pendingId !== null}
              className="flex w-full items-center gap-3 px-4 py-3 text-left transition-colors first:rounded-t-2xl last:rounded-b-2xl hover:bg-slate-50 disabled:opacity-60 dark:hover:bg-slate-800"
            >
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary-50 text-primary-600 dark:bg-primary-900/30 dark:text-primary-400">
                <Building2 className="h-4 w-4" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-slate-900 dark:text-slate-100">{b.businessName}</p>
                <p className="text-xs text-slate-500 dark:text-slate-400">{b.roleName}</p>
              </div>
              <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" />
            </button>
          ))}
        </Card>
      </div>
    </div>
  )
}
