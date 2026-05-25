import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { apiClient } from '@/lib/api-client'
import { projectsQueryKeys } from '@/features/projects/api/query-keys'
import { favoritesQueryKeys } from '@/features/favorites'
import { useUiStore } from '@/stores/uiStore'
import type {
  Guid,
  PaginatedResponse,
  Project,
  ProjectActivity,
  ProjectMember,
  ProjectRole,
  SwimlaneView,
} from '@/lib/types'

export type CreateProjectRequest = {
  name: string
}

export type UpdateProjectRequest = {
  name: string
}

export type InviteUserRequest = {
  email: string
  role: ProjectRole
}

export type UpdateMemberRoleRequest = {
  role: ProjectRole
}

export type TransferOwnershipRequest = {
  newOwnerUserId: Guid
}

export type InvitationCreatedResponse = {
  message: string
}

type PagedParams = {
  page?: number
  pageSize?: number
}

const OWNER_CANNOT_LEAVE_MESSAGE =
  "You can't leave a workspace you own. Transfer ownership to another member first, then try again."

function extractLeaveErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status

    if (status === 403 || status === 500) {
      return OWNER_CANNOT_LEAVE_MESSAGE
    }

    const data = error.response?.data as
      | { error?: { message?: string }; message?: string }
      | undefined
    const serverMessage = data?.error?.message ?? data?.message
    if (serverMessage) {
      return serverMessage
    }
  }

  return 'Unable to leave the workspace. Please try again.'
}

export async function getProjects({ page = 1, pageSize = 25 }: PagedParams): Promise<PaginatedResponse<Project>> {
  const response = await apiClient.get<PaginatedResponse<Project>>('/api/projects', {
    params: {
      page,
      pageSize,
    },
  })

  return response.data
}

export async function getArchivedProjects({
  page = 1,
  pageSize = 25,
}: PagedParams): Promise<PaginatedResponse<Project>> {
  const response = await apiClient.get<PaginatedResponse<Project>>('/api/projects/archived', {
    params: {
      page,
      pageSize,
    },
  })

  return response.data
}

export async function getProject(id: Guid): Promise<Project> {
  const response = await apiClient.get<Project>(`/api/projects/${id}`)
  return response.data
}

export async function createProject(request: CreateProjectRequest): Promise<Project> {
  const response = await apiClient.post<Project>('/api/projects', request)
  return response.data
}

export async function updateProject(id: Guid, request: UpdateProjectRequest): Promise<Project> {
  const response = await apiClient.put<Project>(`/api/projects/${id}`, request)
  return response.data
}

export async function archiveProject(id: Guid): Promise<void> {
  await apiClient.delete(`/api/projects/${id}`)
}

export async function restoreProject(id: Guid): Promise<void> {
  await apiClient.post(`/api/projects/${id}/restore`)
}

export async function purgeProject(id: Guid): Promise<void> {
  await apiClient.delete(`/api/projects/${id}/permanent`)
}

export async function leaveProject(id: Guid): Promise<void> {
  await apiClient.post(`/api/projects/${id}/leave`)
}

export async function getProjectMembers(projectId: Guid): Promise<ProjectMember[]> {
  const response = await apiClient.get<ProjectMember[]>(`/api/projects/${projectId}/members`)
  return response.data
}

export async function inviteUser(projectId: Guid, request: InviteUserRequest): Promise<InvitationCreatedResponse> {
  const response = await apiClient.post<InvitationCreatedResponse>(`/api/projects/${projectId}/invite`, request)
  return response.data
}

export async function updateMemberRole(
  projectId: Guid,
  userId: Guid,
  request: UpdateMemberRoleRequest,
): Promise<ProjectMember> {
  const response = await apiClient.put<ProjectMember>(`/api/projects/${projectId}/members/${userId}/role`, request)
  return response.data
}

export async function removeMember(projectId: Guid, userId: Guid): Promise<void> {
  await apiClient.delete(`/api/projects/${projectId}/members/${userId}`)
}

export async function transferOwnership(
  projectId: Guid,
  request: TransferOwnershipRequest,
): Promise<void> {
  await apiClient.post(`/api/projects/${projectId}/transfer-ownership`, request)
}

export async function getSwimlaneView(projectId: Guid): Promise<SwimlaneView> {
  const response = await apiClient.get<SwimlaneView>(`/api/projects/${projectId}/swimlane`)
  return response.data
}

export function useProjects(page = 1) {
  return useQuery({
    queryKey: [...projectsQueryKeys.projects, page],
    queryFn: () => getProjects({ page }),
  })
}

export function useArchivedProjects(page = 1) {
  return useQuery({
    queryKey: [...projectsQueryKeys.projects, 'archived', page],
    queryFn: () => getArchivedProjects({ page }),
  })
}

export function useAllProjects(includeArchived = true) {
  return useQuery({
    queryKey: [...projectsQueryKeys.projects, 'all', includeArchived],
    queryFn: async () => {
      const allProjects: Project[] = []

      let page = 1
      while (true) {
        const response = await getProjects({ page })
        allProjects.push(...response.items)

        const totalPages = Math.max(1, Math.ceil(response.totalCount / response.pageSize))
        if (page >= totalPages) {
          break
        }

        page += 1
      }

      if (includeArchived) {
        page = 1

        while (true) {
          const response = await getArchivedProjects({ page })
          allProjects.push(...response.items)

          const totalPages = Math.max(1, Math.ceil(response.totalCount / response.pageSize))
          if (page >= totalPages) {
            break
          }

          page += 1
        }
      }

      return allProjects
    },
  })
}

export function useProject(id: Guid | undefined) {
  return useQuery({
    queryKey: id ? projectsQueryKeys.project(id) : [...projectsQueryKeys.projects, 'detail'],
    queryFn: () => getProject(id as Guid),
    enabled: Boolean(id),
  })
}

export function useCreateProject() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: createProject,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useUpdateProject() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: Guid; data: UpdateProjectRequest }) => updateProject(id, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.project(variables.id) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projectSwimlane(variables.id) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useArchiveProject() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => archiveProject(id),
    onSuccess: (_, id) => {
      queryClient.removeQueries({ queryKey: projectsQueryKeys.project(id) })
      queryClient.removeQueries({ queryKey: projectsQueryKeys.projectSwimlane(id) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
      queryClient.invalidateQueries({ queryKey: favoritesQueryKeys.favorites })
    },
  })
}

export function useLeaveProject() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => leaveProject(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
    onError: (error: unknown) => {
      useUiStore.getState().enqueueToast({
        message: extractLeaveErrorMessage(error),
        severity: 'error',
      })
    },
  })
}

export function useRestoreProject() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => restoreProject(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.project(id) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projectSwimlane(id) })
    },
  })
}

export function usePurgeProject() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => purgeProject(id),
    onSuccess: (_, id) => {
      queryClient.removeQueries({ queryKey: projectsQueryKeys.project(id) })
      queryClient.removeQueries({ queryKey: projectsQueryKeys.projectSwimlane(id) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
      queryClient.invalidateQueries({ queryKey: favoritesQueryKeys.favorites })
    },
  })
}

export function useProjectMembers(projectId: Guid | undefined) {
  return useQuery({
    queryKey: projectId ? projectsQueryKeys.projectMembers(projectId) : [...projectsQueryKeys.projects, 'members'],
    queryFn: () => getProjectMembers(projectId as Guid),
    enabled: Boolean(projectId),
  })
}

export function useInviteUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ projectId, data }: { projectId: Guid; data: InviteUserRequest }) =>
      inviteUser(projectId, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projectMembers(variables.projectId) })
    },
  })
}

export function useUpdateMemberRole() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      projectId,
      userId,
      data,
    }: {
      projectId: Guid
      userId: Guid
      data: UpdateMemberRoleRequest
    }) => updateMemberRole(projectId, userId, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projectMembers(variables.projectId) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.project(variables.projectId) })
    },
  })
}

export function useRemoveMember() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ projectId, userId }: { projectId: Guid; userId: Guid }) =>
      removeMember(projectId, userId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projectMembers(variables.projectId) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.project(variables.projectId) })
    },
  })
}

export function useTransferOwnership() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ projectId, data }: { projectId: Guid; data: TransferOwnershipRequest }) =>
      transferOwnership(projectId, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projectMembers(variables.projectId) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.project(variables.projectId) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useSwimlaneView(projectId: Guid | undefined) {
  return useQuery({
    queryKey: projectId ? projectsQueryKeys.projectSwimlane(projectId) : [...projectsQueryKeys.projects, 'swimlane'],
    queryFn: () => getSwimlaneView(projectId as Guid),
    enabled: Boolean(projectId),
  })
}

export async function getProjectActivities(projectId: Guid, limit = 30): Promise<ProjectActivity[]> {
  const response = await apiClient.get<ProjectActivity[]>(`/api/projects/${projectId}/activities`, {
    params: { limit },
  })
  return response.data
}

export function useProjectActivities(projectId: Guid | undefined) {
  return useQuery({
    queryKey: projectId ? projectsQueryKeys.projectActivities(projectId) : [...projectsQueryKeys.projects, 'activities'],
    queryFn: () => getProjectActivities(projectId as Guid),
    enabled: Boolean(projectId),
  })
}
