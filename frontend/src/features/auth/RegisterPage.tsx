import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useAuth } from '@/contexts/AuthContext'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'

const schema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  email: z.string().min(1, 'Email is required').email('Enter a valid email address'),
  password: z
    .string()
    .min(8, 'Must be at least 8 characters')
    .regex(/[A-Z]/, 'Must include an uppercase letter')
    .regex(/[a-z]/, 'Must include a lowercase letter')
    .regex(/[0-9]/, 'Must include a number'),
})

type FormValues = z.infer<typeof schema>

export function RegisterPage() {
  const { register: registerUser } = useAuth()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const onSubmit = async (values: FormValues) => {
    setServerError(null)
    try {
      await registerUser(values.email, values.password, values.firstName, values.lastName)
      navigate('/onboarding', { replace: true })
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    }
  }

  return (
    <Card className="p-6">
      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <div className="grid grid-cols-2 gap-3">
          <FormField label="First name" htmlFor="firstName" error={errors.firstName?.message}>
            <Input
              id="firstName"
              autoComplete="given-name"
              {...register('firstName')}
              error={errors.firstName?.message}
            />
          </FormField>
          <FormField label="Last name" htmlFor="lastName" error={errors.lastName?.message}>
            <Input
              id="lastName"
              autoComplete="family-name"
              {...register('lastName')}
              error={errors.lastName?.message}
            />
          </FormField>
        </div>

        <FormField label="Email" htmlFor="email" error={errors.email?.message}>
          <Input id="email" type="email" autoComplete="email" {...register('email')} error={errors.email?.message} />
        </FormField>

        <FormField
          label="Password"
          htmlFor="password"
          error={errors.password?.message}
          hint="At least 8 characters, with an uppercase letter, lowercase letter, and number."
        >
          <Input
            id="password"
            type="password"
            autoComplete="new-password"
            {...register('password')}
            error={errors.password?.message}
          />
        </FormField>

        <Button type="submit" isLoading={isSubmitting} className="w-full">
          Create account
        </Button>
      </form>

      <p className="mt-6 text-center text-sm text-slate-500 dark:text-slate-400">
        Already have an account?{' '}
        <Link to="/login" className="font-medium text-primary-600 hover:text-primary-700">
          Sign in
        </Link>
      </p>
    </Card>
  )
}
