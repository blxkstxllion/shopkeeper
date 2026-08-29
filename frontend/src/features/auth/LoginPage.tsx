import { useEffect, useRef, useState, type InputHTMLAttributes, type ReactNode } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { clsx } from 'clsx'
import {
  ShieldCheck,
  Check,
  Download,
  ChevronDown,
  Smartphone,
  Monitor,
  Mail,
  Lock,
  Eye,
  EyeOff,
  ArrowRight,
  Loader2,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/contexts/AuthContext'
import { Logo } from '@/components/ui/Logo'
import { Input, FormField } from '@/components/ui/Input'
import { DigitCodeInput } from '@/components/ui/DigitCodeInput'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'
import type { User } from '@/types/auth'

function DownloadMenu({ className, variant = 'light' }: { className: string; variant?: 'light' | 'dark' }) {
  const [open, setOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  return (
    <div className={className} ref={menuRef}>
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-haspopup="true"
        aria-expanded={open}
        className={
          variant === 'dark'
            ? 'flex items-center gap-1.5 rounded-lg border border-white/20 bg-white/10 px-3 py-1.5 text-sm font-medium text-white shadow-sm backdrop-blur-sm hover:bg-white/20'
            : 'flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 shadow-sm hover:bg-slate-50'
        }
      >
        <Download className="h-3.5 w-3.5" />
        Download app
        <ChevronDown className={variant === 'dark' ? 'h-3.5 w-3.5 text-white/70' : 'h-3.5 w-3.5 text-slate-400'} />
      </button>

      {open && (
        <div className="absolute right-0 top-full z-20 mt-1 w-52 rounded-lg border border-slate-200 bg-white py-1 shadow-lg">
          <a
            href="/downloads/ShopKeeper.apk"
            className="flex items-center gap-2 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50"
          >
            <Smartphone className="h-4 w-4" />
            Android (APK)
          </a>
          <a
            href="/downloads/ShopKeeper-Setup-x64.exe"
            className="flex items-center gap-2 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50"
          >
            <Monitor className="h-4 w-4" />
            Windows (.exe)
          </a>
        </div>
      )}
    </div>
  )
}

// Ambient background for the brand panel: a faint drifting grid, two slow-pulsing glow
// blobs, a handful of floating geometric outlines, and particles drifting upward. All
// motion is decorative-only (aria-hidden via pointer-events-none + no interactive content)
// and gets neutralized globally by the prefers-reduced-motion rule in index.css.
function BrandAmbience({ compact = false }: { compact?: boolean }) {
  const particleCount = compact ? 4 : 10
  return (
    <div className="pointer-events-none absolute inset-0 overflow-hidden">
      <div
        className="absolute inset-0 animate-[grid-pan_18s_linear_infinite] opacity-[0.07]"
        style={{
          backgroundImage:
            'linear-gradient(to right, white 1px, transparent 1px), linear-gradient(to bottom, white 1px, transparent 1px)',
          backgroundSize: '48px 48px',
        }}
      />

      <div className="absolute -left-16 -top-16 h-72 w-72 animate-[pulse-glow_7s_ease-in-out_infinite] rounded-full bg-primary-400 blur-3xl" />
      <div
        className={clsx(
          'absolute animate-[pulse-glow_9s_ease-in-out_infinite] rounded-full bg-emerald-300 blur-3xl',
          compact ? 'right-0 top-0 h-40 w-40' : '-bottom-24 right-0 h-96 w-96',
        )}
        style={{ animationDelay: '2s' }}
      />

      {!compact && (
        <>
          <div className="absolute right-24 top-48 h-10 w-10 animate-[float_6s_ease-in-out_infinite] rounded-lg border border-white/20 [transform:rotate(12deg)]" />
          <div
            className="absolute bottom-40 left-24 h-6 w-6 animate-[float-slow_7s_ease-in-out_infinite] rounded-md border border-white/20"
            style={{ animationDelay: '1s' }}
          />
          <div
            className="absolute bottom-10 right-1/3 h-4 w-4 animate-[float_5s_ease-in-out_infinite] rounded-full bg-white/20"
            style={{ animationDelay: '0.5s' }}
          />
        </>
      )}

      {Array.from({ length: particleCount }).map((_, i) => (
        <span
          key={i}
          className="absolute bottom-0 h-1 w-1 animate-[drift_6s_ease-in-out_infinite] rounded-full bg-white/60"
          style={{
            left: `${(i * 37) % 100}%`,
            animationDelay: `${i * 0.7}s`,
            animationDuration: `${6 + (i % 4)}s`,
          }}
        />
      ))}
    </div>
  )
}

// Decorative mock dashboard cards - static illustrative numbers (not live data), styled
// and positioned to sell "this is a business dashboard" at a glance. Gated to xl+ screens
// so it never fights the heading/bullets for space on a smaller desktop window.
function DashboardCards() {
  return (
    <div className="relative mt-10 hidden h-64 w-full xl:block">
      <div className="absolute left-0 top-0 w-44 animate-[float_7s_ease-in-out_infinite] rounded-2xl border border-white/15 bg-white/10 p-4 shadow-xl backdrop-blur-md">
        <p className="text-xs text-primary-100/80">Total Profit</p>
        <p className="mt-1 text-lg font-semibold text-white">$24,530</p>
        <svg viewBox="0 0 100 30" className="mt-2 h-8 w-full overflow-visible">
          <polyline
            points="0,25 15,20 30,22 45,12 60,15 75,6 100,2"
            fill="none"
            stroke="#6ee7b7"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeDasharray="240"
            className="animate-[draw-line_1.8s_ease-out_0.6s_forwards]"
            style={{ strokeDashoffset: 240 }}
          />
        </svg>
        <span className="mt-1 inline-block text-[11px] font-medium text-primary-300">+12.5%</span>
      </div>

      <div
        className="absolute right-0 top-4 w-36 animate-[float-slow_8s_ease-in-out_infinite] rounded-2xl border border-white/15 bg-white/10 p-4 shadow-xl backdrop-blur-md"
        style={{ animationDelay: '0.4s' }}
      >
        <p className="text-xs text-primary-100/80">Orders</p>
        <p className="mt-1 text-lg font-semibold text-white">1,642</p>
        <span className="mt-1 inline-block text-[11px] font-medium text-primary-300">+8.2%</span>
      </div>

      <div
        className="absolute bottom-0 right-12 w-40 animate-[float_9s_ease-in-out_infinite] rounded-2xl border border-white/15 bg-white/10 p-4 shadow-xl backdrop-blur-md"
        style={{ animationDelay: '1.2s' }}
      >
        <p className="text-xs text-primary-100/80">Inventory</p>
        <p className="mt-1 text-lg font-semibold text-white">
          98 <span className="text-xs font-normal text-primary-200">in stock</span>
        </p>
      </div>
    </div>
  )
}

function IconField({
  icon: Icon,
  error,
  rightAdornment,
  className,
  ...props
}: {
  icon: LucideIcon
  error?: string
  rightAdornment?: ReactNode
} & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <div className="relative">
      <Icon className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
      <input
        {...props}
        className={clsx(
          'h-11 w-full rounded-xl border bg-white/70 pl-10 text-sm text-slate-900 placeholder:text-slate-400 backdrop-blur-sm transition-all duration-200',
          'focus:border-primary-500 focus:bg-white focus:outline-none focus:ring-4 focus:ring-primary-500/15',
          rightAdornment ? 'pr-10' : 'pr-3',
          error ? 'border-red-400 focus:border-red-500 focus:ring-red-500/15' : 'border-slate-200',
          className,
        )}
      />
      {rightAdornment && <div className="absolute right-3 top-1/2 -translate-y-1/2">{rightAdornment}</div>}
    </div>
  )
}

function SignInButton({ isLoading, children }: { isLoading: boolean; children: ReactNode }) {
  return (
    <button
      type="submit"
      disabled={isLoading}
      className={clsx(
        'group relative flex h-12 w-full items-center justify-center gap-2 overflow-hidden rounded-xl',
        'bg-gradient-to-r from-primary-600 via-primary-500 to-primary-600 bg-[length:200%_100%] bg-[position:0%_0%]',
        'text-sm font-semibold text-white shadow-lg shadow-primary-600/20 transition-all duration-300',
        'hover:-translate-y-0.5 hover:bg-[position:100%_0%] hover:shadow-xl hover:shadow-primary-600/30',
        'active:translate-y-0 active:shadow-md',
        'disabled:cursor-not-allowed disabled:opacity-70 disabled:hover:translate-y-0',
      )}
    >
      {isLoading ? (
        <Loader2 className="h-4 w-4 animate-spin" />
      ) : (
        <>
          {children}
          <ArrowRight className="h-4 w-4 transition-transform duration-300 group-hover:translate-x-1" />
        </>
      )}
    </button>
  )
}

function GlassCard({ children }: { children: ReactNode }) {
  return (
    <div className="rounded-3xl border border-white/60 bg-white/80 p-7 shadow-2xl shadow-slate-900/10 backdrop-blur-xl sm:p-8">
      {children}
    </div>
  )
}

const credentialsSchema = z.object({
  email: z.string().min(1, 'Email is required').email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
})

type CredentialsFormValues = z.infer<typeof credentialsSchema>

const codeSchema = z.object({
  code: z.string().min(6, 'Enter the 6-digit code, or a recovery code').max(20),
})

type CodeFormValues = z.infer<typeof codeSchema>

const BRAND_HIGHLIGHTS = [
  'Real-time sales, inventory, and profit tracking',
  'Multi-branch support built in from day one',
  'Role-based access for your whole team',
]

export function LoginPage() {
  const { login, completeTwoFactorLogin } = useAuth()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const redirectTo = searchParams.get('redirect')
  const [serverError, setServerError] = useState<string | null>(null)
  const [challengeToken, setChallengeToken] = useState<string | null>(null)
  const [useRecoveryCode, setUseRecoveryCode] = useState(false)
  const [showPassword, setShowPassword] = useState(false)

  const credentialsForm = useForm<CredentialsFormValues>({ resolver: zodResolver(credentialsSchema) })
  const codeForm = useForm<CodeFormValues>({ resolver: zodResolver(codeSchema) })
  const codeValue = codeForm.watch('code') ?? ''

  const goToNextScreen = (user: User) => {
    if (redirectTo) {
      navigate(redirectTo, { replace: true })
    } else if (user.businesses.length === 0) {
      navigate('/onboarding', { replace: true })
    } else if (user.businesses.length === 1) {
      navigate('/app', { replace: true })
    } else {
      navigate('/select-business', { replace: true })
    }
  }

  const onSubmitCredentials = async (values: CredentialsFormValues) => {
    setServerError(null)
    try {
      const outcome = await login(values.email, values.password)
      if (outcome.requiresTwoFactor) {
        setChallengeToken(outcome.challengeToken)
      } else {
        goToNextScreen(outcome.user)
      }
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    }
  }

  const onSubmitCode = async (values: CodeFormValues) => {
    setServerError(null)
    try {
      const user = await completeTwoFactorLogin(challengeToken!, values.code)
      goToNextScreen(user)
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    }
  }

  return (
    <div className="flex min-h-screen flex-col bg-slate-50 lg:flex-row">
      {/* Mobile: compact animated header instead of a shrunk copy of the desktop panel */}
      <div className="relative overflow-hidden bg-gradient-to-br from-primary-800 via-primary-700 to-primary-900 px-5 pb-7 pt-5 text-white lg:hidden">
        <BrandAmbience compact />
        <div className="relative z-10 flex items-center justify-between gap-2">
          <div className="flex animate-[fade-up_0.5s_ease-out_both] items-center gap-2">
            <Logo className="h-9 w-9" />
            <span className="text-base font-semibold">The Shop Keeper</span>
          </div>
          <DownloadMenu className="relative" variant="dark" />
        </div>
        <p
          className="relative z-10 mt-3 animate-[fade-up_0.5s_ease-out_both] text-sm text-primary-100"
          style={{ animationDelay: '0.1s' }}
        >
          Know your business. Grow your profit.
        </p>
      </div>

      {/* Desktop brand panel */}
      <div className="relative hidden w-1/2 flex-col justify-between overflow-hidden bg-gradient-to-br from-primary-900 via-primary-800 to-primary-950 p-12 text-white lg:flex">
        <BrandAmbience />
        <DownloadMenu className="absolute right-12 top-12 z-20" variant="dark" />

        <div className="relative z-10 flex animate-[fade-up_0.6s_ease-out_both] items-center gap-2">
          <Logo className="h-10 w-10" />
          <span className="text-lg font-semibold">The Shop Keeper</span>
        </div>

        <div className="relative z-10">
          <span
            className="mb-4 inline-flex animate-[fade-up_0.6s_ease-out_both] items-center gap-1.5 rounded-full border border-white/20 bg-white/10 px-3 py-1 text-xs font-medium text-primary-100"
            style={{ animationDelay: '0.05s' }}
          >
            <span className="h-1.5 w-1.5 rounded-full bg-primary-300" />
            All-in-one solution
          </span>
          <h2
            className="mb-6 animate-[fade-up_0.6s_ease-out_both] text-3xl font-semibold leading-tight"
            style={{ animationDelay: '0.15s' }}
          >
            Know your business. Grow your profit.
          </h2>
          <ul className="space-y-3">
            {BRAND_HIGHLIGHTS.map((highlight, i) => (
              <li
                key={highlight}
                className="flex animate-[fade-up_0.6s_ease-out_both] items-center gap-2.5 text-sm text-primary-100"
                style={{ animationDelay: `${0.25 + i * 0.08}s` }}
              >
                <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-white/15">
                  <Check className="h-3 w-3" />
                </span>
                {highlight}
              </li>
            ))}
          </ul>

          <DashboardCards />
        </div>

        <p className="relative z-10 text-xs text-primary-200">&copy; {new Date().getFullYear()} The Shop Keeper</p>
      </div>

      <div className="flex w-full flex-1 flex-col items-center justify-center px-4 py-10 lg:w-1/2 lg:py-12">
        <div className="w-full max-w-sm animate-[fade-up_0.6s_ease-out_both]" style={{ animationDelay: '0.15s' }}>
          {challengeToken ? (
            <GlassCard>
              <div className="mb-5 flex flex-col items-center gap-2 text-center">
                <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-primary-100">
                  <ShieldCheck className="h-6 w-6 text-primary-600" />
                </span>
                <h1 className="text-lg font-semibold text-slate-900">Two-factor verification</h1>
                <p className="text-sm text-slate-500">
                  Enter the 6-digit code from your authenticator app, or one of your recovery codes.
                </p>
              </div>

              <form onSubmit={codeForm.handleSubmit(onSubmitCode)} className="flex flex-col gap-4">
                {serverError && <Alert tone="error">{serverError}</Alert>}

                {useRecoveryCode ? (
                  <FormField label="Recovery code" htmlFor="code" error={codeForm.formState.errors.code?.message}>
                    <Input
                      id="code"
                      autoFocus
                      autoComplete="one-time-code"
                      placeholder="Enter a recovery code"
                      {...codeForm.register('code')}
                      error={codeForm.formState.errors.code?.message}
                    />
                  </FormField>
                ) : (
                  <div className="flex flex-col items-center gap-1.5">
                    <DigitCodeInput
                      length={6}
                      value={codeValue}
                      onChange={(v) => codeForm.setValue('code', v, { shouldValidate: true })}
                      error={Boolean(codeForm.formState.errors.code)}
                      autoFocus
                    />
                    {codeForm.formState.errors.code && (
                      <p className="text-xs text-red-600">{codeForm.formState.errors.code.message}</p>
                    )}
                  </div>
                )}

                <SignInButton isLoading={codeForm.formState.isSubmitting}>Verify and sign in</SignInButton>
                <button
                  type="button"
                  onClick={() => {
                    setUseRecoveryCode((v) => !v)
                    codeForm.setValue('code', '')
                    codeForm.clearErrors('code')
                  }}
                  className="text-center text-sm font-medium text-primary-600 hover:text-primary-700"
                >
                  {useRecoveryCode ? 'Use an authenticator code instead' : 'Use a recovery code instead'}
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setChallengeToken(null)
                    setServerError(null)
                  }}
                  className="text-center text-sm font-medium text-slate-500 hover:text-slate-700"
                >
                  Back to sign in
                </button>
              </form>
            </GlassCard>
          ) : (
            <GlassCard>
              <div className="mb-6 text-center">
                <h1 className="text-xl font-semibold text-slate-900">
                  Welcome back <span aria-hidden="true">👋</span>
                </h1>
                <p className="mt-1 text-sm text-slate-500">Sign in to continue to your account</p>
              </div>

              <form onSubmit={credentialsForm.handleSubmit(onSubmitCredentials)} className="flex flex-col gap-4">
                {serverError && <Alert tone="error">{serverError}</Alert>}

                <div className="flex flex-col gap-1.5">
                  <label htmlFor="email" className="text-sm font-medium text-slate-700">
                    Email
                  </label>
                  <IconField
                    icon={Mail}
                    id="email"
                    type="email"
                    autoComplete="email"
                    placeholder="you@example.com"
                    error={credentialsForm.formState.errors.email?.message}
                    {...credentialsForm.register('email')}
                  />
                  {credentialsForm.formState.errors.email && (
                    <p className="text-xs text-red-600">{credentialsForm.formState.errors.email.message}</p>
                  )}
                </div>

                <div className="flex flex-col gap-1.5">
                  <div className="flex items-center justify-between">
                    <label htmlFor="password" className="text-sm font-medium text-slate-700">
                      Password
                    </label>
                    <Link to="/forgot-password" className="text-xs font-medium text-primary-600 hover:text-primary-700">
                      Forgot password?
                    </Link>
                  </div>
                  <IconField
                    icon={Lock}
                    id="password"
                    type={showPassword ? 'text' : 'password'}
                    autoComplete="current-password"
                    placeholder="••••••••"
                    error={credentialsForm.formState.errors.password?.message}
                    rightAdornment={
                      <button
                        type="button"
                        tabIndex={-1}
                        onClick={() => setShowPassword((v) => !v)}
                        aria-label={showPassword ? 'Hide password' : 'Show password'}
                        className="text-slate-400 hover:text-slate-600"
                      >
                        {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                      </button>
                    }
                    {...credentialsForm.register('password')}
                  />
                  {credentialsForm.formState.errors.password && (
                    <p className="text-xs text-red-600">{credentialsForm.formState.errors.password.message}</p>
                  )}
                </div>

                <SignInButton isLoading={credentialsForm.formState.isSubmitting}>Sign in</SignInButton>
              </form>

              <p className="mt-6 text-center text-sm text-slate-500">
                Don&apos;t have an account?{' '}
                <Link to="/register" className="font-medium text-primary-600 hover:text-primary-700">
                  Create one
                </Link>
              </p>
            </GlassCard>
          )}
        </div>
      </div>
    </div>
  )
}
