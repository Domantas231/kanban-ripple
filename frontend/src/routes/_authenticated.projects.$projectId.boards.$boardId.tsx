import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { BoardView } from '@/features/boards'

export const Route = createFileRoute('/_authenticated/projects/$projectId/boards/$boardId')({
  validateSearch: z.object({
    cardId: z.string().optional(),
    tagIds: z.string().optional(),
    userIds: z.string().optional(),
    due: z.enum(['overdue', 'today', 'week', 'none']).optional(),
    assign: z.enum(['me', 'unassigned', 'multiple']).optional(),
    activity: z.enum(['24h', '7d', '30d', 'stale']).optional(),
    createdByIds: z.string().optional(),
    hasAttachments: z.enum(['1', '0']).optional(),
    hasComments: z.enum(['1', '0']).optional(),
    estMin: z.coerce.number().optional(),
    estMax: z.coerce.number().optional(),
  }),
  loader: ({ params }) => ({ projectId: params.projectId, boardId: params.boardId }),
  component: BoardRoute,
})

function BoardRoute() {
  const { projectId, boardId } = Route.useLoaderData()
  const search = Route.useSearch()
  return <BoardView projectId={projectId} boardId={boardId} search={search} />
}
