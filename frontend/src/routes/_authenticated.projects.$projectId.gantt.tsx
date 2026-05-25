import { createFileRoute, useRouterState } from '@tanstack/react-router'
import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import { GanttView } from '@/features/gantt'

export const Route = createFileRoute('/_authenticated/projects/$projectId/gantt')({
  component: GanttRoutePage,
})

function GanttRoutePage() {
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  const projectId = pathname.match(/^\/projects\/([^/]+)/)?.[1] ?? ''

  return (
    <Box
      sx={{
        px: { xs: 1.5, sm: 2, md: 3 },
        pb: { xs: 'calc(env(safe-area-inset-bottom, 0px) + 36px)', sm: 2, md: 3 },
        pt: { xs: 1.5, sm: 2, md: 3 },
      }}
    >
      <Box sx={{ mb: 4 }}>
        <Typography variant="h3" sx={{ fontWeight: 700, mb: 0.5 }}>
          Timeline
        </Typography>
        <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 520 }}>
          Visualize your workspace schedule and track progress across boards.
        </Typography>
      </Box>
      <GanttView projectId={projectId} />
    </Box>
  )
}
