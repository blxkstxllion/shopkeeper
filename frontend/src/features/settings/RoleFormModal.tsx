import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { createRole, getPermissionCatalog, updateRole } from '@/api/roles'
import { ApiError } from '@/lib/api-client'
import type { RoleManagement } from '@/types/role'

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  description: z.string().max(500).optional().or(z.literal('')),
})

type FormValues = z.infer<typeof schema>

const defaults: FormValues = { name: '', description: '' }

export function RoleFormModal({
  isOpen,
  onClose,
  role,
}: {
  isOpen: boolean
  onClose: () => void
  role?: RoleManagement | null
}) {
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)
  const [permissionKeys, setPermissionKeys] = useState<string[]>([])
  const isEditing = Boolean(role)

  const { data: catalog } = useQuery({
    queryKey: ['permission-catalog'],
    queryFn: getPermissionCatalog,
    enabled: isOpen,
  })

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    reset,
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: role ? { name: role.name, description: role.description ?? '' } : defaults,
  })

  // Re-seeds the checked permissions whenever a different role is opened for editing, or the
  // modal reopens fresh for "New role" - without this, the previous role's selections would
  // linger after switching rows, same pattern as ProductFormModal's image-preview reset.
  useEffect(() => {
    if (isOpen) {
      setPermissionKeys(role?.permissionKeys ?? [])
      setServerError(null)
    }
  }, [isOpen, role])

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      const payload = { name: values.name, description: values.description || null, permissionKeys }
      if (isEditing && role) {
        await updateRole({ ...payload, id: role.id })
      } else {
        await createRole(payload)
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['role-management'] })
      reset(defaults)
      onClose()
    },
    onError: (err) =>
      setServerError(err instanceof ApiError ? err.message : 'Unable to save this role. Please try again.'),
  })

  const categories = Array.from(new Set(catalog?.map((p) => p.category) ?? []))

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={isEditing ? 'Edit role' : 'New role'} size="lg">
      <form onSubmit={handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <FormField label="Name" htmlFor="name" error={errors.name?.message}>
          <Input id="name" {...register('name')} error={errors.name?.message} />
        </FormField>

        <FormField label="Description (optional)" htmlFor="description" error={errors.description?.message}>
          <Input id="description" {...register('description')} error={errors.description?.message} />
        </FormField>

        <div>
          <p className="mb-2 text-sm font-medium text-slate-700 dark:text-slate-300">Permissions</p>
          <div className="flex flex-col gap-3">
            {categories.map((category) => (
              <div key={category}>
                <p className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-slate-400">{category}</p>
                <div className="grid grid-cols-2 gap-1.5">
                  {catalog
                    ?.filter((p) => p.category === category)
                    .map((p) => (
                      <label key={p.key} className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
                        <input
                          type="checkbox"
                          checked={permissionKeys.includes(p.key)}
                          onChange={() =>
                            setPermissionKeys((prev) =>
                              prev.includes(p.key) ? prev.filter((k) => k !== p.key) : [...prev, p.key],
                            )
                          }
                          className="h-4 w-4 rounded border-slate-300 text-primary-600 focus:ring-primary-500"
                        />
                        {p.name}
                      </label>
                    ))}
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-2 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isSubmitting || mutation.isPending}>
            {isEditing ? 'Save changes' : 'Create role'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
