import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import * as authApi from '@/api/auth'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'

const schema = z.object({
  newPassword: z
    .string()
    .min(8, 'Must be at least 8 characters')
    .regex(/[A-Z]/, 'Must include an uppercase letter')
    .regex(/[a-z]/, 'Must include a lowercase letter')
    .regex(/[0-9]/, 'Must include a number'),
})

type FormValues = z.infer<typeof schema>

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token')
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  if (!token) {
    return (
      <Card className="p-6">
        <h1 className="mb-4 text-center text-lg font-semibold text-slate-900 dark:text-slate-100">
          Reset your password
        </h1>
        <Alert tone="error">This password reset link is missing its token. Please request a new one.</Alert>
        <Link
          to="/forgot-password"
          className="mt-6 block text-center text-sm font-medium text-primary-600 hover:text-primary-700"
        >
          Request a new link
        </Link>
      </Card>
    )
  }

  const onSubmit = async (values: FormValues) => {
    setServerError(null)
    try {
      await authApi.resetPassword(token, values.newPassword)
      navigate('/login', { replace: true, state: { passwordReset: true } })
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    }
  }

  return (
    <Card className="p-6">
      <h1 className="mb-4 text-center text-lg font-semibold text-slate-900 dark:text-slate-100">Reset your password</h1>
      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <FormField label="New password" htmlFor="newPassword" error={errors.newPassword?.message}>
          <Input
            id="newPassword"
            type="password"
            autoComplete="new-password"
            {...register('newPassword')}
            error={errors.newPassword?.message}
          />
        </FormField>

        <Button type="submit" isLoading={isSubmitting} className="w-full">
          Reset password
        </Button>
      </form>
    </Card>
  )
}
