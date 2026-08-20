import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import * as authApi from '@/api/auth'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'

const schema = z.object({
  email: z.string().min(1, 'Email is required').email('Enter a valid email address'),
})

type FormValues = z.infer<typeof schema>

export function ForgotPasswordPage() {
  const [submitted, setSubmitted] = useState(false)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const onSubmit = async (values: FormValues) => {
    // Always shows the same success state regardless of whether the email exists - avoids account enumeration.
    await authApi.forgotPassword(values.email)
    setSubmitted(true)
  }

  if (submitted) {
    return (
      <Card className="p-6">
        <h1 className="mb-4 text-center text-lg font-semibold text-slate-900 dark:text-slate-100">
          Forgot your password?
        </h1>
        <Alert tone="success">
          If an account with that email exists, we&apos;ve sent a link to reset your password.
        </Alert>
        <Link
          to="/login"
          className="mt-6 block text-center text-sm font-medium text-primary-600 hover:text-primary-700"
        >
          Back to sign in
        </Link>
      </Card>
    )
  }

  return (
    <Card className="p-6">
      <h1 className="mb-4 text-center text-lg font-semibold text-slate-900 dark:text-slate-100">
        Forgot your password?
      </h1>
      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Enter the email associated with your account and we&apos;ll send you a link to reset your password.
        </p>

        <FormField label="Email" htmlFor="email" error={errors.email?.message}>
          <Input id="email" type="email" autoComplete="email" {...register('email')} error={errors.email?.message} />
        </FormField>

        <Button type="submit" isLoading={isSubmitting} className="w-full">
          Send reset link
        </Button>
      </form>

      <p className="mt-6 text-center text-sm text-slate-500 dark:text-slate-400">
        <Link to="/login" className="font-medium text-primary-600 hover:text-primary-700">
          Back to sign in
        </Link>
      </p>
    </Card>
  )
}
