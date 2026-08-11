import { useBranchContext } from '@/contexts/BranchContext'
import type { Branch } from '@/types/business'

/** Thin wrapper over BranchContext for pages that only need "which branch am I operating in". */
export function useActiveBranch(): { branch: Branch | null; isLoading: boolean } {
  const { activeBranch, isLoading } = useBranchContext()
  return { branch: activeBranch, isLoading }
}
