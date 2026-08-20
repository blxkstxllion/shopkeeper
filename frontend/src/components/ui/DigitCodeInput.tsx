import { useRef } from 'react'
import { clsx } from 'clsx'

interface DigitCodeInputProps {
  length?: number
  value: string
  onChange: (value: string) => void
  error?: boolean
  autoFocus?: boolean
}

/** Six-box one-time-code entry: digits only, auto-advances, backspace steps back, and a full paste fans out across the boxes. */
export function DigitCodeInput({ length = 6, value, onChange, error, autoFocus }: DigitCodeInputProps) {
  const inputRefs = useRef<(HTMLInputElement | null)[]>([])
  const digits = Array.from({ length }, (_, i) => value[i] ?? '')

  function setDigitAt(index: number, digit: string) {
    const next = digits.slice()
    next[index] = digit
    onChange(next.join(''))
  }

  function handleChange(index: number, raw: string) {
    const cleaned = raw.replace(/\D/g, '')
    if (!cleaned) {
      setDigitAt(index, '')
      return
    }
    if (cleaned.length > 1) {
      const next = digits.slice()
      for (let i = 0; i < cleaned.length && index + i < length; i++) {
        next[index + i] = cleaned[i]
      }
      onChange(next.join(''))
      const lastFilled = Math.min(index + cleaned.length, length - 1)
      inputRefs.current[lastFilled]?.focus()
      return
    }
    setDigitAt(index, cleaned)
    if (index < length - 1) inputRefs.current[index + 1]?.focus()
  }

  function handleKeyDown(index: number, e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Backspace' && !digits[index] && index > 0) {
      inputRefs.current[index - 1]?.focus()
    } else if (e.key === 'ArrowLeft' && index > 0) {
      inputRefs.current[index - 1]?.focus()
    } else if (e.key === 'ArrowRight' && index < length - 1) {
      inputRefs.current[index + 1]?.focus()
    }
  }

  function handlePaste(e: React.ClipboardEvent<HTMLInputElement>) {
    const pasted = e.clipboardData.getData('text').replace(/\D/g, '')
    if (!pasted) return
    e.preventDefault()
    onChange(pasted.slice(0, length))
    const lastFilled = Math.min(pasted.length, length) - 1
    inputRefs.current[Math.max(lastFilled, 0)]?.focus()
  }

  return (
    <div className="flex justify-center gap-2" role="group" aria-label="Verification code">
      {digits.map((digit, i) => (
        <input
          key={i}
          ref={(el) => {
            inputRefs.current[i] = el
          }}
          type="text"
          inputMode="numeric"
          autoComplete={i === 0 ? 'one-time-code' : 'off'}
          autoFocus={autoFocus && i === 0}
          maxLength={length}
          value={digit}
          onChange={(e) => handleChange(i, e.target.value)}
          onKeyDown={(e) => handleKeyDown(i, e)}
          onPaste={handlePaste}
          aria-label={`Digit ${i + 1} of ${length}`}
          className={clsx(
            'h-12 w-10 rounded-lg border bg-white text-center text-lg font-semibold text-slate-900',
            'focus:outline-none focus:ring-2 focus:ring-primary-500/40 focus:border-primary-500',
            'dark:bg-slate-900 dark:text-slate-100',
            error
              ? 'border-red-400 focus:ring-red-500/30 focus:border-red-500'
              : 'border-slate-300 dark:border-slate-600',
          )}
        />
      ))}
    </div>
  )
}
