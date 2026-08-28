import { apiClient } from '@/lib/api-client'
import type { AdvisorAnswer, AdvisorCapabilities, AdvisorQuestion, AdvisorQuestionId } from '@/types/advisor'

export async function getAdvisorQuestions(): Promise<AdvisorQuestion[]> {
  const { data } = await apiClient.get<AdvisorQuestion[]>('/advisor/questions')
  return data
}

export async function getAdvisorAnswer(questionId: AdvisorQuestionId): Promise<AdvisorAnswer> {
  const { data } = await apiClient.get<AdvisorAnswer>('/advisor/answer', { params: { questionId } })
  return data
}

export async function getAdvisorCapabilities(): Promise<AdvisorCapabilities> {
  const { data } = await apiClient.get<AdvisorCapabilities>('/advisor/capabilities')
  return data
}

export async function askAdvisor(question: string): Promise<AdvisorAnswer> {
  const { data } = await apiClient.post<AdvisorAnswer>('/advisor/ask', { question })
  return data
}
