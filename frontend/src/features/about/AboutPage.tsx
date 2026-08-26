import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Store, Trophy, TrendingDown, Pencil } from 'lucide-react'
import { getBusinessAbout, updateBusinessAbout } from '@/api/about'
import { useSessionClaims } from '@/hooks/useSessionClaims'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { StatTile } from '@/components/ui/StatTile'
import { Alert } from '@/components/ui/Alert'
import { FormSkeleton } from '@/components/ui/Skeleton'
import { ApiError } from '@/lib/api-client'
import { formatMoney, resolveUploadUrl } from '@/lib/format'

const TEXTAREA_CLASS =
  'w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100'

export function AboutPage() {
  const claims = useSessionClaims()
  const canEdit = Boolean(claims?.isOwner || claims?.permissions.includes('settings:manage'))
  const queryClient = useQueryClient()
  const { data, isLoading } = useQuery({ queryKey: ['business-about'], queryFn: getBusinessAbout })

  const [isEditing, setIsEditing] = useState(false)
  const [description, setDescription] = useState('')
  const [ownerBio, setOwnerBio] = useState('')
  const [serverError, setServerError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  useEffect(() => {
    if (data) {
      setDescription(data.description ?? '')
      setOwnerBio(data.ownerBio ?? '')
    }
  }, [data])

  const mutation = useMutation({
    mutationFn: () =>
      updateBusinessAbout({ description: description.trim() || null, ownerBio: ownerBio.trim() || null }),
    onSuccess: () => {
      setServerError(null)
      setSuccessMessage('About page updated.')
      setIsEditing(false)
      queryClient.invalidateQueries({ queryKey: ['business-about'] })
      setTimeout(() => setSuccessMessage(null), 3000)
    },
    onError: (err) =>
      setServerError(err instanceof ApiError ? err.message : 'Unable to save changes. Please try again.'),
  })

  if (isLoading || !data) {
    return (
      <div className="mx-auto max-w-3xl">
        <Card className="p-6">
          <FormSkeleton fields={3} />
        </Card>
      </div>
    )
  }

  const salesByYear = data.salesByYear
  // 0 years: section omitted entirely below (no fake $0 "best year"). 1 year: a lone data point
  // can't meaningfully be "best" or "worst", so bestYear/worstYear only apply at 2+.
  const bestYear = salesByYear.length >= 2 ? salesByYear.reduce((a, b) => (b.revenue > a.revenue ? b : a)) : null
  const worstYear = salesByYear.length >= 2 ? salesByYear.reduce((a, b) => (b.revenue < a.revenue ? b : a)) : null

  return (
    <div className="mx-auto max-w-3xl">
      <div className="mb-6 flex items-center gap-3">
        {data.logoUrl ? (
          <img src={resolveUploadUrl(data.logoUrl)} alt="" className="h-12 w-12 rounded-lg object-cover" />
        ) : (
          <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-primary-100 text-primary-700 dark:bg-primary-900/40 dark:text-primary-300">
            <Store className="h-6 w-6" />
          </div>
        )}
        <div>
          <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">{data.businessName}</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">About this shop</p>
        </div>
      </div>

      <Card className="mb-6 p-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}
        {successMessage && <Alert tone="success">{successMessage}</Alert>}

        {isEditing ? (
          <form
            onSubmit={(e) => {
              e.preventDefault()
              mutation.mutate()
            }}
            className="flex flex-col gap-4"
          >
            <div>
              <label
                htmlFor="description"
                className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300"
              >
                About the shop
              </label>
              <textarea
                id="description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                maxLength={2000}
                rows={4}
                className={TEXTAREA_CLASS}
                placeholder="What does your shop sell? What makes it special?"
              />
            </div>
            <div>
              <label htmlFor="ownerBio" className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">
                About the owner
              </label>
              <textarea
                id="ownerBio"
                value={ownerBio}
                onChange={(e) => setOwnerBio(e.target.value)}
                maxLength={2000}
                rows={4}
                className={TEXTAREA_CLASS}
                placeholder="Tell customers and employees a bit about yourself"
              />
            </div>
            <div className="flex justify-end gap-2">
              <Button type="button" variant="secondary" onClick={() => setIsEditing(false)}>
                Cancel
              </Button>
              <Button type="submit" isLoading={mutation.isPending}>
                Save
              </Button>
            </div>
          </form>
        ) : (
          <div className="flex flex-col gap-4">
            <div>
              <h2 className="mb-1 text-sm font-semibold text-slate-900 dark:text-slate-100">About the shop</h2>
              <p className="whitespace-pre-wrap text-sm text-slate-600 dark:text-slate-300">
                {data.description || 'No description yet.'}
              </p>
            </div>
            <div>
              <h2 className="mb-1 text-sm font-semibold text-slate-900 dark:text-slate-100">About the owner</h2>
              <p className="whitespace-pre-wrap text-sm text-slate-600 dark:text-slate-300">
                {data.ownerBio || 'No owner bio yet.'}
              </p>
            </div>
            {canEdit && (
              <div className="flex justify-end">
                <Button type="button" variant="secondary" onClick={() => setIsEditing(true)}>
                  <Pencil className="h-4 w-4" />
                  Edit
                </Button>
              </div>
            )}
          </div>
        )}
      </Card>

      {salesByYear.length > 0 && (
        <Card className="p-4">
          <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">Achievements</h2>

          {salesByYear.length === 1 ? (
            <StatTile
              label={`Sales in ${salesByYear[0].year}`}
              icon={Trophy}
              value={formatMoney(salesByYear[0].revenue)}
            />
          ) : (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              {bestYear && (
                <StatTile label={`Best year - ${bestYear.year}`} icon={Trophy} value={formatMoney(bestYear.revenue)} />
              )}
              {worstYear && (
                <StatTile
                  label={`Toughest year - ${worstYear.year}`}
                  icon={TrendingDown}
                  value={formatMoney(worstYear.revenue)}
                />
              )}
            </div>
          )}

          {salesByYear.length > 1 && (
            <div className="mt-4 flex flex-col gap-1.5 border-t border-slate-100 pt-4 dark:border-slate-800">
              {[...salesByYear].reverse().map((y) => (
                <div key={y.year} className="flex items-center justify-between text-sm">
                  <span className="text-slate-500 dark:text-slate-400">{y.year}</span>
                  <span className="font-medium text-slate-900 dark:text-slate-100">{formatMoney(y.revenue)}</span>
                </div>
              ))}
            </div>
          )}
        </Card>
      )}
    </div>
  )
}
