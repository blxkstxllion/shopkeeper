import { useQuery } from '@tanstack/react-query'
import { getBranches } from '@/api/branches'
import type { Branch } from '@/types/business'

/**
 * Resolves which branch POS/inventory actions apply to. There's no branch-switcher UI yet
 * (that's a later multi-branch polish item), so this defaults to the business's main branch -
 * correct for the common single-branch case, and a clearly-named single place to upgrade
 * once branch switching exists.
 */
export function useActiveBranch(): { branch: Branch | null; isLoading: boolean } {
  const { data: branches, isLoading } = useQuery({ queryKey: ['branches'], queryFn: getBranches })
  const branch = branches?.find((b) => b.isMainBranch) ?? branches?.[0] ?? null
  return { branch, isLoading }
}
