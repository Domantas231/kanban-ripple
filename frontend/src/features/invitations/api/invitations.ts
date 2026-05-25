import { apiClient } from '@/lib/api-client'

export async function acceptInvitation(token: string): Promise<void> {
  await apiClient.post(`/api/invitations/${encodeURIComponent(token)}/accept`)
}
