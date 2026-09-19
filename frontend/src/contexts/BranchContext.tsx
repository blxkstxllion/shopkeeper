import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { getBranches } from '@/api/branches'
import { useAuth } from '@/contexts/AuthContext'
import { useSessionClaims } from '@/hooks/useSessionClaims'
import { useOfflineListQuery } from '@/offline/useOfflineQuery'
import type { Branch } from '@/types/business'

interface BranchContextValue {
  branches: Branch[]
  activeBranchId: string | null
  activeBranch: Branch | null
  setActiveBranchId: (id: string) => void
  /** False for roles pinned to one branch (Cashier, Branch Manager) - the backend enforces
   * this independently, but hiding the picker avoids offering a choice that would just 403. */
  canSwitchBranches: boolean
  isLoading: boolean
}

const BranchContext = createContext<BranchContextValue | null>(null)

function storageKey(businessId: string) {
  return `shopkeeper:activeBranch:${businessId}`
}

export function BranchProvider({ children }: { children: ReactNode }) {
  const claims = useSessionClaims()
  const { activeBusiness } = useAuth()
  // Gated on activeBusiness (restored from the offline session snapshot on a cold start),
  // not claims.businessId - claims come from decoding the access token, which is
  // deliberately never persisted to disk (see session-cache.ts), so on a cold start with no
  // network to redeem a fresh one, claims stays null forever and this query would never run
  // at all, even though useOfflineListQuery below already has everything it needs to serve
  // cached branches from activeBusiness alone. This was the actual cause of "Loading
  // branch..." never resolving on a fully offline app restart.
  const { data: branches, isLoading } = useOfflineListQuery<Branch>(
    ['branches'],
    'branches',
    getBranches,
    Boolean(activeBusiness?.businessId),
  )

  const [selectedId, setSelectedId] = useState<string | null>(null)

  const restrictedBranchId = claims?.branchId ?? null
  const businessId = activeBusiness?.businessId ?? null

  // Same fix as the query above, applied here too - this effect is what actually sets
  // selectedId (activeBranch), and it was still gated on claims.businessId even after
  // fixing the query itself, so the branches LIST loaded from cache fine offline but
  // nothing that needs an active branch (Sell, Inventory) ever picked one.
  useEffect(() => {
    if (!branches || branches.length === 0 || !businessId) return

    if (restrictedBranchId) {
      setSelectedId(restrictedBranchId)
      return
    }

    const stored = localStorage.getItem(storageKey(businessId))
    const storedIsValid = stored && branches.some((b) => b.id === stored)
    if (storedIsValid) {
      setSelectedId(stored)
      return
    }

    setSelectedId(branches.find((b) => b.isMainBranch)?.id ?? branches[0].id)
  }, [branches, businessId, restrictedBranchId])

  const setActiveBranchId = (id: string) => {
    setSelectedId(id)
    if (businessId) {
      localStorage.setItem(storageKey(businessId), id)
    }
  }

  const activeBranch = useMemo(() => branches?.find((b) => b.id === selectedId) ?? null, [branches, selectedId])

  const value: BranchContextValue = {
    branches: branches ?? [],
    activeBranchId: selectedId,
    activeBranch,
    setActiveBranchId,
    canSwitchBranches: !restrictedBranchId && (branches?.length ?? 0) > 1,
    isLoading,
  }

  return <BranchContext.Provider value={value}>{children}</BranchContext.Provider>
}

export function useBranchContext() {
  const ctx = useContext(BranchContext)
  if (!ctx) throw new Error('useBranchContext must be used within a BranchProvider')
  return ctx
}
