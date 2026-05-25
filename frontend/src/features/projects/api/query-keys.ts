export const projectsQueryKeys = {
  projects: ['projects'] as const,
  project: (id: string) => ['projects', id] as const,
  projectMembers: (id: string) => ['projects', id, 'members'] as const,
  projectSwimlane: (id: string) => ['projects', id, 'swimlane'] as const,
  projectActivities: (id: string) => ['projects', id, 'activities'] as const,
} as const
