import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Sparkles, Loader2 } from 'lucide-react'
import { getAdvisorAnswer, getAdvisorQuestions } from '@/api/advisor'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/ui/EmptyState'
import { UpgradePrompt } from '@/components/ui/UpgradePrompt'
import { ApiError } from '@/lib/api-client'
import { formatDateTime } from '@/lib/format'
import type { AdvisorQuestion } from '@/types/advisor'

interface TranscriptEntry {
  id: string
  question: string
  answer?: string
  askedAt: string
  isError?: boolean
}

export function AdvisorPage() {
  const [transcript, setTranscript] = useState<TranscriptEntry[]>([])

  const {
    data: questions,
    isLoading: questionsLoading,
    error: questionsError,
  } = useQuery({
    queryKey: ['advisor-questions'],
    queryFn: getAdvisorQuestions,
  })

  const mutation = useMutation({
    mutationFn: getAdvisorAnswer,
    onSuccess: (result, questionId) => {
      const label = questions?.find((q) => q.id === questionId)?.label ?? questionId
      setTranscript((prev) => [
        ...prev,
        {
          id: `${questionId}-${result.generatedAt}`,
          question: label,
          answer: result.answer,
          askedAt: result.generatedAt,
        },
      ])
    },
    onError: (_err, questionId) => {
      const label = questions?.find((q) => q.id === questionId)?.label ?? questionId
      setTranscript((prev) => [
        ...prev,
        {
          id: `${questionId}-${Date.now()}`,
          question: label,
          answer: "Couldn't get an answer for that just now. Please try again.",
          askedAt: new Date().toISOString(),
          isError: true,
        },
      ])
    },
  })

  function handleAsk(question: AdvisorQuestion) {
    mutation.mutate(question.id)
  }

  if (questionsError instanceof ApiError && questionsError.status === 403) {
    return (
      <UpgradePrompt
        title="The AI Advisor isn't available on your current plan"
        description="Upgrade to a Business + AI or Enterprise + AI plan to unlock instant answers about your business."
      />
    )
  }

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <div>
        <h1 className="flex items-center gap-2 text-xl font-semibold text-slate-900 dark:text-slate-100">
          <Sparkles className="h-5 w-5 text-primary-600" />
          The Shop Keeper Advisor
        </h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Quick answers about your business, calculated from your own sales, stock, and expense data.
        </p>
      </div>

      <Card className="p-4">
        <p className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Ask a question</p>
        <div className="flex flex-wrap gap-2">
          {questionsLoading && <Loader2 className="h-5 w-5 animate-spin text-slate-400" />}
          {questions?.map((q) => (
            <button
              key={q.id}
              type="button"
              onClick={() => handleAsk(q)}
              disabled={mutation.isPending}
              className="rounded-full border border-slate-200 px-3 py-1.5 text-sm text-slate-700 transition-colors hover:border-primary-400 hover:text-primary-700 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-300 dark:hover:border-primary-500 dark:hover:text-primary-400"
            >
              {q.label}
            </button>
          ))}
        </div>
      </Card>

      {transcript.length === 0 ? (
        <EmptyState
          icon={Sparkles}
          title="No questions asked yet"
          description="Pick one of the questions above to get an instant answer based on your real numbers."
        />
      ) : (
        <div className="flex flex-col gap-4">
          {transcript.map((entry) => (
            <div key={entry.id} className="flex flex-col gap-2">
              <div className="ml-auto max-w-[80%] rounded-2xl rounded-tr-sm bg-primary-600 px-4 py-2 text-sm text-white">
                {entry.question}
              </div>
              <div
                className={`mr-auto max-w-[80%] rounded-2xl rounded-tl-sm px-4 py-2 text-sm ${
                  entry.isError
                    ? 'bg-red-50 text-red-700 dark:bg-red-900/30 dark:text-red-400'
                    : 'bg-slate-100 text-slate-800 dark:bg-slate-800 dark:text-slate-200'
                }`}
              >
                <p>{entry.answer}</p>
                <p className="mt-1 text-xs text-slate-400 dark:text-slate-500">{formatDateTime(entry.askedAt)}</p>
              </div>
            </div>
          ))}
          {mutation.isPending && (
            <div className="mr-auto flex items-center gap-2 rounded-2xl rounded-tl-sm bg-slate-100 px-4 py-2 text-sm text-slate-500 dark:bg-slate-800 dark:text-slate-400">
              <Loader2 className="h-4 w-4 animate-spin" />
              Calculating…
            </div>
          )}
        </div>
      )}
    </div>
  )
}
