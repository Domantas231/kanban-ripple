export const plannerQueryKeys = {
  plannerBlocks: (projectId: string, date: string) =>
    ['planner', projectId, 'blocks', date] as const,
  plannerUnscheduled: (projectId: string, date: string) =>
    ['planner', projectId, 'unscheduled', date] as const,
  googleStatus: ['google', 'status'] as const,
  googleCalendarEvents: (date: string) => ['google', 'calendar', 'events', date] as const,
} as const
