import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import QRCode from 'qrcode'
import { Check, Copy, QrCode } from 'lucide-react'
import { getJoinCode, regenerateJoinCode, revokeJoinCode } from '@/api/employees'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'

export function JoinCodeCard() {
  const queryClient = useQueryClient()
  const [copied, setCopied] = useState(false)
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null)

  const { data: code, isLoading } = useQuery({ queryKey: ['join-code'], queryFn: getJoinCode })

  useEffect(() => {
    if (!code) {
      setQrDataUrl(null)
      return
    }
    const joinUrl = `${window.location.origin}/join?code=${code}`
    QRCode.toDataURL(joinUrl).then(setQrDataUrl)
  }, [code])

  const regenerateMutation = useMutation({
    mutationFn: regenerateJoinCode,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['join-code'] }),
  })

  const revokeMutation = useMutation({
    mutationFn: revokeJoinCode,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['join-code'] }),
  })

  function copyCode() {
    if (!code) return
    navigator.clipboard.writeText(code)
    setCopied(true)
    setTimeout(() => setCopied(false), 1500)
  }

  return (
    <Card className="mb-6 p-4">
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-slate-100 text-slate-400 dark:bg-slate-800">
            <QrCode className="h-5 w-5" />
          </div>
          <div>
            <h3 className="text-sm font-semibold text-slate-900 dark:text-slate-100">Join code</h3>
            <p className="text-sm text-slate-500 dark:text-slate-400">
              Let workers request to join by scanning a QR code or typing a PIN. Every request still needs your approval
              before they get access.
            </p>
          </div>
        </div>
      </div>

      {!isLoading && (
        <div className="mt-4">
          {code ? (
            <div className="flex flex-wrap items-start gap-4">
              {qrDataUrl && (
                <img
                  src={qrDataUrl}
                  alt="Join code QR"
                  className="h-32 w-32 rounded-lg border border-slate-200 dark:border-slate-700"
                />
              )}
              <div className="flex flex-col gap-2">
                <div className="flex items-center gap-2 rounded-lg bg-slate-50 px-3 py-2 dark:bg-slate-800">
                  <code className="text-lg font-semibold tracking-widest text-slate-700 dark:text-slate-300">
                    {code}
                  </code>
                  <button
                    type="button"
                    onClick={copyCode}
                    className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                  >
                    {copied ? <Check className="h-3.5 w-3.5" /> : <Copy className="h-3.5 w-3.5" />}
                  </button>
                </div>
                <div className="flex gap-2">
                  <Button
                    variant="secondary"
                    size="sm"
                    isLoading={regenerateMutation.isPending}
                    onClick={() => regenerateMutation.mutate()}
                  >
                    Regenerate
                  </Button>
                  <Button
                    variant="danger"
                    size="sm"
                    isLoading={revokeMutation.isPending}
                    onClick={() => revokeMutation.mutate()}
                  >
                    Revoke
                  </Button>
                </div>
                <p className="text-xs text-slate-400">Regenerating invalidates this code immediately.</p>
              </div>
            </div>
          ) : (
            <Button size="sm" isLoading={regenerateMutation.isPending} onClick={() => regenerateMutation.mutate()}>
              Generate a join code
            </Button>
          )}
        </div>
      )}
    </Card>
  )
}
