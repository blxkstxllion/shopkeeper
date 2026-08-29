import { useEffect, useState, type CSSProperties } from 'react'
import { clsx } from 'clsx'
import { X } from 'lucide-react'
import { useTour } from './TourContext'
import { tourSteps } from './tourSteps'

export function TourOverlay() {
  const { isActive, step, stepIndex, totalSteps, next, back, skip } = useTour()
  const [rect, setRect] = useState<DOMRect | null>(null)

  useEffect(() => {
    if (!isActive || !step?.target) {
      setRect(null)
      return
    }
    const target = step.target
    function updateRect() {
      const el = document.querySelector(target)
      setRect(el ? el.getBoundingClientRect() : null)
    }
    updateRect()
    window.addEventListener('resize', updateRect)
    window.addEventListener('scroll', updateRect, true)
    return () => {
      window.removeEventListener('resize', updateRect)
      window.removeEventListener('scroll', updateRect, true)
    }
  }, [isActive, step])

  if (!isActive || !step) return null

  const padding = 8
  const spotlightStyle: CSSProperties = rect
    ? {
        position: 'fixed',
        top: rect.top - padding,
        left: rect.left - padding,
        width: rect.width + padding * 2,
        height: rect.height + padding * 2,
        borderRadius: 12,
        boxShadow: '0 0 0 9999px rgba(15, 23, 42, 0.7)',
        transition: 'top 0.3s ease, left 0.3s ease, width 0.3s ease, height 0.3s ease',
        pointerEvents: 'none',
        zIndex: 100,
      }
    : { position: 'fixed', inset: 0, background: 'rgba(15, 23, 42, 0.7)', zIndex: 100 }

  const tooltipStyle: CSSProperties = rect
    ? {
        position: 'fixed',
        top: Math.min(rect.bottom + padding + 12, window.innerHeight - 220),
        left: Math.min(Math.max(rect.left, 16), window.innerWidth - 336),
        zIndex: 101,
      }
    : { position: 'fixed', top: '50%', left: '50%', transform: 'translate(-50%, -50%)', zIndex: 101 }

  return (
    <>
      <div style={spotlightStyle} />
      <div
        style={tooltipStyle}
        className="w-80 rounded-2xl border border-white/10 bg-slate-900 p-5 text-white shadow-2xl"
        role="dialog"
        aria-modal="true"
        aria-label={step.title}
      >
        <div className="mb-1 flex items-center justify-between">
          <span className="text-xs font-medium text-primary-300">
            Step {stepIndex + 1} of {totalSteps}
          </span>
          <button type="button" onClick={skip} aria-label="Skip tour" className="text-slate-400 hover:text-white">
            <X className="h-4 w-4" />
          </button>
        </div>
        <h3 className="mb-1 text-base font-semibold">{step.title}</h3>
        <p className="mb-4 text-sm text-slate-300">{step.body}</p>
        <div className="flex items-center justify-between">
          <button
            type="button"
            onClick={back}
            disabled={stepIndex === 0}
            className="text-sm text-slate-400 hover:text-white disabled:opacity-30"
          >
            Back
          </button>
          <div className="flex gap-1">
            {tourSteps.map((s, i) => (
              <span
                key={s.id}
                className={clsx('h-1.5 w-1.5 rounded-full', i === stepIndex ? 'bg-primary-400' : 'bg-white/20')}
              />
            ))}
          </div>
          <button
            type="button"
            onClick={next}
            className="rounded-lg bg-primary-600 px-3 py-1.5 text-sm font-medium hover:bg-primary-500"
          >
            {stepIndex + 1 === totalSteps ? 'Finish' : 'Next'}
          </button>
        </div>
      </div>
    </>
  )
}
