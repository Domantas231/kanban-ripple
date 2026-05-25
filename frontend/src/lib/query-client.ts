import { QueryClient } from '@tanstack/react-query'
import type { AxiosError } from 'axios'

function shouldRetry(failureCount: number, error: Error): boolean {
  const status = (error as AxiosError)?.response?.status
  if (status === 404 || status === 403 || status === 401) return false
  return failureCount < 3
}

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: shouldRetry,
    },
    mutations: {
      retry: 1,
    },
  },
})
