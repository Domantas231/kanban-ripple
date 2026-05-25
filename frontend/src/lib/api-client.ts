import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { useUiStore } from '@/stores/uiStore'
import type { AuthResult } from './types'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

export const apiClient = axios.create({
  baseURL: apiBaseUrl,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
})

type RetryableRequestConfig = InternalAxiosRequestConfig & {
  _retry?: boolean
}

type ApiAuthBridge = {
  getAccessToken: () => string | null
  applyRefreshedSession: (user: { id: string; email: string }, accessToken: string) => void
  clearSession: () => void
}

let authBridge: ApiAuthBridge = {
  getAccessToken: () => null,
  applyRefreshedSession: () => {},
  clearSession: () => {},
}

export function configureApiClientAuth(bridge: ApiAuthBridge): void {
  authBridge = bridge
}

type ApiNavigationBridge = {
  redirectToWorkspaces: () => void
}

let navigationBridge: ApiNavigationBridge = {
  redirectToWorkspaces: () => {
    if (typeof window !== 'undefined' && window.location.pathname !== '/projects') {
      window.location.assign('/projects')
    }
  },
}

export function configureApiClientNavigation(bridge: ApiNavigationBridge): void {
  navigationBridge = bridge
}

function getRetryAfterSeconds(error: AxiosError): number | null {
  const retryAfterHeader = error.response?.headers?.['retry-after']

  if (!retryAfterHeader) {
    return null
  }

  const firstValue = String(retryAfterHeader).split(',')[0]?.trim()
  const asNumber = Number(firstValue)

  if (!Number.isNaN(asNumber) && Number.isFinite(asNumber) && asNumber >= 0) {
    return Math.floor(asNumber)
  }

  const retryDateMs = Date.parse(firstValue)

  if (Number.isNaN(retryDateMs)) {
    return null
  }

  const deltaSeconds = Math.ceil((retryDateMs - Date.now()) / 1000)
  return deltaSeconds > 0 ? deltaSeconds : 0
}

function shouldSkipGlobalStatusHandling(requestUrl: string): boolean {
  return requestUrl.includes('/api/auth/')
}

const PROJECT_SCOPED_GET_PATTERN = /\/api\/(projects|boards)\/[0-9a-fA-F-]{36}/

function isLostProjectAccess(error: AxiosError, requestUrl: string): boolean {
  if (typeof window === 'undefined') {
    return false
  }

  const method = (error.config?.method ?? 'get').toLowerCase()
  if (method !== 'get') {
    return false
  }

  if (!PROJECT_SCOPED_GET_PATTERN.test(requestUrl)) {
    return false
  }

  return /^\/projects\/[^/]+/.test(window.location.pathname)
}

function handleGlobalStatusEffects(error: AxiosError, requestUrl: string): void {
  if (shouldSkipGlobalStatusHandling(requestUrl)) {
    return
  }

  const status = error.response?.status

  if (status === 403) {
    const serverMessage = (error.response?.data as { error?: { message?: string } } | undefined)?.error?.message
    const accessLost = isLostProjectAccess(error, requestUrl)
    useUiStore.getState().enqueueToast({
      message: serverMessage ?? (accessLost ? 'You no longer have access to this workspace.' : 'Access Denied'),
      severity: 'warning',
    })
    if (accessLost) {
      navigationBridge.redirectToWorkspaces()
    }
    return
  }

  if (status === 404) {
    return
  }

  if (status === 409) {
    const errorBody = (error.response?.data as { error?: { code?: string; message?: string } } | undefined)?.error
    if (errorBody?.code === 'DUPLICATE_NAME') {
      useUiStore.getState().enqueueToast({
        message: errorBody.message ?? 'This name is already in use.',
        severity: 'error',
      })
      return
    }
    useUiStore
      .getState()
      .openConflictDialog('Conflict: refresh and try again.')
    return
  }

  if (status === 429) {
    const retryAfterSeconds = getRetryAfterSeconds(error)
    const retryAfterText =
      retryAfterSeconds === null
        ? ''
        : ` Retry after ${retryAfterSeconds} second${retryAfterSeconds === 1 ? '' : 's'}.`

    useUiStore.getState().enqueueToast({
      message: `Rate limited, please wait.${retryAfterText}`,
      severity: 'warning',
      durationMs: 7000,
    })
  }
}

let refreshPromise: Promise<AuthResult> | null = null

async function performRefreshRequest(): Promise<AuthResult> {
  const response = await axios.post<AuthResult>(
    '/api/auth/refresh',
    undefined,
    {
      withCredentials: true,
      headers: {
        'Content-Type': 'application/json',
      },
    },
  )

  return response.data
}

async function refreshSession(): Promise<AuthResult> {
  if (!refreshPromise) {
    refreshPromise = performRefreshRequest().finally(() => {
      refreshPromise = null
    })
  }

  return refreshPromise
}

function redirectToLogin(): void {
  if (typeof window === 'undefined') {
    return
  }

  if (window.location.pathname !== '/login') {
    window.location.assign('/login')
  }
}

apiClient.interceptors.request.use((config) => {
  const accessToken = authBridge.getAccessToken()

  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`
  }

  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as RetryableRequestConfig | undefined
    const responseStatus = error.response?.status
    const requestUrl = originalRequest?.url ?? ''
    const isAuthRequest = requestUrl.includes('/api/auth/')

    if (responseStatus !== 401 || !originalRequest || originalRequest._retry || isAuthRequest) {
      handleGlobalStatusEffects(error, requestUrl)
      return Promise.reject(error)
    }

    originalRequest._retry = true

    try {
      const refreshed = await refreshSession()

      authBridge.applyRefreshedSession(
        {
          id: refreshed.userId,
          email: refreshed.email,
        },
        refreshed.accessToken,
      )

      originalRequest.headers.Authorization = `Bearer ${refreshed.accessToken}`
      return apiClient(originalRequest)
    } catch {
      authBridge.clearSession()
      redirectToLogin()
      return Promise.reject(error)
    }
  },
)
