import { apiClient } from '@/lib/api-client'
import type {
  Guid,
  PlannedBlock,
  CreateBlockData,
  UpdateBlockData,
  UnscheduledCard,
  GoogleCalendarEvent,
} from '@/lib/types'

export async function getPlannedBlocks(projectId: Guid, date: string): Promise<PlannedBlock[]> {
  const response = await apiClient.get<PlannedBlock[]>(
    `/api/projects/${projectId}/planner/blocks`,
    { params: { date } },
  )
  return response.data
}

export async function createBlock(projectId: Guid, data: CreateBlockData): Promise<PlannedBlock> {
  const response = await apiClient.post<PlannedBlock>(
    `/api/projects/${projectId}/planner/blocks`,
    data,
  )
  return response.data
}

export async function updateBlock(
  projectId: Guid,
  blockId: Guid,
  data: UpdateBlockData,
): Promise<PlannedBlock> {
  const response = await apiClient.put<PlannedBlock>(
    `/api/projects/${projectId}/planner/blocks/${blockId}`,
    data,
  )
  return response.data
}

export async function deleteBlock(projectId: Guid, blockId: Guid): Promise<void> {
  await apiClient.delete(`/api/projects/${projectId}/planner/blocks/${blockId}`)
}

export async function getUnscheduledCards(
  projectId: Guid,
  date: string,
): Promise<UnscheduledCard[]> {
  const response = await apiClient.get<UnscheduledCard[]>(
    `/api/projects/${projectId}/planner/unscheduled`,
    { params: { date } },
  )
  return response.data
}

export async function getGoogleCalendarEvents(date: string): Promise<GoogleCalendarEvent[]> {
  const response = await apiClient.get<GoogleCalendarEvent[]>(
    '/api/google/calendar/events',
    { params: { date } },
  )
  return response.data
}
