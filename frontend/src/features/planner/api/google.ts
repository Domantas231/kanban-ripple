import { apiClient } from '@/lib/api-client'
import type { DriveSharePermission, Guid, GoogleConnectionStatus, GoogleDriveLink, LinkFilesResult, PermissionRevokeReport } from '@/lib/types'

export async function getGoogleStatus(): Promise<GoogleConnectionStatus> {
  const response = await apiClient.get<GoogleConnectionStatus>('/api/google/status')
  return response.data
}

export async function getGoogleAuthUrl(): Promise<string> {
  const response = await apiClient.get<{ url: string }>('/api/google/auth')
  return response.data.url
}

export async function disconnectGoogle(): Promise<void> {
  await apiClient.delete('/api/google/disconnect')
}

export async function getGooglePickerToken(): Promise<string> {
  const response = await apiClient.get<{ accessToken: string }>('/api/google/picker-token')
  return response.data.accessToken
}

export async function getCardGoogleDriveLinks(cardId: Guid): Promise<GoogleDriveLink[]> {
  const response = await apiClient.get<GoogleDriveLink[]>(`/api/cards/${cardId}/google-drive-links`)
  return response.data
}

export async function linkGoogleDriveFiles(cardId: Guid, googleFileIds: string[], sharePermission: DriveSharePermission = 'reader'): Promise<LinkFilesResult> {
  const response = await apiClient.post<LinkFilesResult>(`/api/cards/${cardId}/google-drive-links`, { googleFileIds, sharePermission })
  return response.data
}

export async function updateDriveLinkPermission(linkId: Guid, sharePermission: DriveSharePermission): Promise<GoogleDriveLink> {
  const response = await apiClient.patch<GoogleDriveLink>(`/api/google-drive-links/${linkId}/permission`, { sharePermission })
  return response.data
}

export async function unlinkGoogleDriveFile(linkId: Guid): Promise<PermissionRevokeReport> {
  const response = await apiClient.delete<PermissionRevokeReport>(`/api/google-drive-links/${linkId}`)
  return response.data
}
