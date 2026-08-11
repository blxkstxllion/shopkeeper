import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getBranches } from '@/api/branches'
import { useSessionClaims } from '@/hooks/useSessionClaims'
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
  const { data: branches, isLoading } = useQuery({
    queryKey: ['branches'],
    queryFn: getBranches,
    enabled: Boolean(claims?.businessId),
  })

  const [selectedId, setSelectedId] = useState<string | null>(null)

  const restrictedBranchId = claims?.branchId ?? null

  useEffect(() => {
    if (!branches || branches.length === 0 || !claims?.businessId) return

    if (restrictedBranchId) {
      setSelectedId(restrictedBranchId)
      return
    }

    const stored = localStorage.getItem(storageKey(claims.businessId))
    const storedIsValid = stored && branches.some((b) => b.id === stored)
    if (storedIsValid) {
      setSelectedId(stored)
      return
    }

    setSelectedId(branches.find((b) => b.isMainBranch)?.id ?? branches[0].id)
  }, [branches, claims?.businessId, restrictedBranchId])

  const setActiveBranchId = (id: string) => {
    setSelectedId(id)
    if (claims?.businessId) {
      localStorage.setItem(storageKey(claims.businessId), id)
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
