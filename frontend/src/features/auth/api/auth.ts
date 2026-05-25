import axios from 'axios'
import { apiClient } from '@/lib/api-client'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { signalRService } from '@/features/realtime'
import type { AuthResult } from '@/lib/types'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

export type RegisterRequest = {
  email: string
  password: string
}

export type LoginRequest = {
  email: string
  password: string
}

export type PasswordResetRequest = {
  email: string
}

export type ResetPasswordRequest = {
  email: string
  token: string
  newPassword: string
}

export type ProfilePhotoResponse = {
  message: string
}

export type PasswordResetResponse = {
  message: string
}

export type ChangePasswordRequest = {
  currentPassword: string
  newPassword: string
}

export type ChangePasswordResponse = {
  message: string
}

export type UpdateDisplayNameRequest = {
  displayName: string
}

export type UpdateDisplayNameResponse = {
  displayName: string
}

export type RegisterResponse = {
  message: string
  email: string
}

export type ConfirmEmailRequest = {
  email: string
  token: string
}

export type ConfirmEmailResponse = {
  message: string
}

export type ResendConfirmationRequest = {
  email: string
}

export type ResendConfirmationResponse = {
  message: string
}


function setSessionFromAuthResult(result: AuthResult): void {
  useAuthStore.getState().setAuth(
    {
      id: result.userId,
      email: result.email,
      userName: result.userName ?? undefined,
    },
    result.accessToken,
  )
}

export async function register(request: RegisterRequest): Promise<RegisterResponse> {
  const response = await apiClient.post<RegisterResponse>('/api/auth/register', request)
  return response.data
}

export async function confirmEmail(request: ConfirmEmailRequest): Promise<ConfirmEmailResponse> {
  const response = await apiClient.post<ConfirmEmailResponse>('/api/auth/confirm-email', request)
  return response.data
}

export async function resendConfirmation(
  request: ResendConfirmationRequest,
): Promise<ResendConfirmationResponse> {
  const response = await apiClient.post<ResendConfirmationResponse>(
    '/api/auth/resend-confirmation',
    request,
  )
  return response.data
}

export async function login(request: LoginRequest): Promise<AuthResult> {
  const response = await apiClient.post<AuthResult>('/api/auth/login', request)
  setSessionFromAuthResult(response.data)
  return response.data
}

export async function logout(): Promise<void> {
  try {
    await apiClient.post('/api/auth/logout', undefined, {
      withCredentials: true,
    })
  } finally {
    await signalRService.disconnect()
    useAuthStore.getState().clearAuth()
  }
}

export async function refresh(): Promise<AuthResult> {
  const response = await axios.post<AuthResult>(
    `${apiBaseUrl}/api/auth/refresh`,
    undefined,
    {
      withCredentials: true,
      headers: {
        'Content-Type': 'application/json',
      },
    },
  )

  setSessionFromAuthResult(response.data)
  return response.data
}

export async function requestPasswordReset(
  request: PasswordResetRequest,
): Promise<PasswordResetResponse> {
  const response = await apiClient.post<PasswordResetResponse>(
    '/api/auth/password-reset',
    request,
  )

  return response.data
}

export async function resetPassword(request: ResetPasswordRequest): Promise<PasswordResetResponse> {
  const response = await apiClient.put<PasswordResetResponse>(
    '/api/auth/password-reset',
    request,
  )

  return response.data
}

export async function changePassword(request: ChangePasswordRequest): Promise<ChangePasswordResponse> {
  const response = await apiClient.put<ChangePasswordResponse>('/api/auth/password', request)
  return response.data
}

export async function updateDisplayName(request: UpdateDisplayNameRequest): Promise<UpdateDisplayNameResponse> {
  const response = await apiClient.put<UpdateDisplayNameResponse>('/api/auth/display-name', request)
  return response.data
}

export async function deleteAccount(): Promise<void> {
  await apiClient.delete('/api/auth/account')
}

export async function uploadProfilePhoto(file: File): Promise<ProfilePhotoResponse> {
  const formData = new FormData()
  formData.append('file', file)
  const response = await apiClient.post<ProfilePhotoResponse>('/api/auth/profile-photo', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return response.data
}

export async function getUserProfilePhoto(userId: string): Promise<string | null> {
  try {
    const response = await apiClient.get(`/api/auth/users/${userId}/profile-photo`, {
      responseType: 'blob',
    })
    if (response.status === 204) return null
    return URL.createObjectURL(response.data as Blob)
  } catch {
    return null
  }
}

export async function deleteProfilePhoto(): Promise<void> {
  await apiClient.delete('/api/auth/profile-photo')
}

