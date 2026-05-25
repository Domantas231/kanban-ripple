import { useNavigate } from '@tanstack/react-router'
import Box from '@mui/material/Box'
import Card from '@mui/material/Card'
import CardActionArea from '@mui/material/CardActionArea'
import CardContent from '@mui/material/CardContent'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import ViewKanbanOutlinedIcon from '@mui/icons-material/ViewKanbanOutlined'
import WarningAmberOutlinedIcon from '@mui/icons-material/WarningAmberOutlined'
import type { BoardSwimlane, Guid } from '@/lib/types'

interface OverviewBoardCardsProps {
  projectId: Guid
  boards: BoardSwimlane[]
}

export function OverviewBoardCards({ projectId, boards }: OverviewBoardCardsProps) {
  const navigate = useNavigate()

  return (
    <Box>
      <Typography variant="h3" sx={{ fontWeight: 700, mb: 2.5 }}>
        Boards
      </Typography>
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: '1fr',
            sm: 'repeat(2, 1fr)',
            md: 'repeat(3, 1fr)',
          },
          gap: 2,
        }}
      >
        {boards.map((boardLane) => {
          const totalCards = boardLane.columns.reduce((sum, col) => sum + col.cardCount, 0)
          const now = new Date()
          const overdueCount = boardLane.columns.reduce(
            (sum, col) =>
              sum +
              (col.cards ?? []).filter((c) => c.dueDate && new Date(c.dueDate) < now).length,
            0,
          )

          return (
            <Card
              key={boardLane.board.id}
              elevation={0}
              sx={{
                display: 'flex',
                flexDirection: 'column',
                border: '1px solid',
                borderColor: 'divider',
                '&:hover': { boxShadow: 2 },
              }}
            >
              <CardActionArea
                onClick={() =>
                  navigate({
                    to: '/projects/$projectId/boards/$boardId',
                    params: { projectId, boardId: boardLane.board.id },
                  })
                }
                sx={{ flex: 1, display: 'flex', flexDirection: 'column', justifyContent: 'flex-start', alignItems: 'stretch' }}
              >
                <CardContent sx={{ p: 2.5, '&:last-child': { pb: 2.5 } }}>
                  <Stack spacing={2}>
                    <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={1} sx={{ minWidth: 0 }}>
                      <Stack direction="row" spacing={1} alignItems="center" sx={{ minWidth: 0, overflow: 'hidden' }}>
                        <ViewKanbanOutlinedIcon
                          sx={{ fontSize: 20, color: 'primary.main', flexShrink: 0 }}
                        />
                        <Typography variant="body1" noWrap sx={{ fontWeight: 700 }}>
                          {boardLane.board.name}
                        </Typography>
                      </Stack>
                      {overdueCount > 0 && (
                        <Chip
                          icon={<WarningAmberOutlinedIcon sx={{ fontSize: '16px !important' }} />}
                          label={`${overdueCount} overdue`}
                          size="small"
                          sx={{
                            height: 24,
                            fontSize: 12,
                            fontWeight: 600,
                            flexShrink: 0,
                            bgcolor: 'error.main',
                            color: '#fff',
                            '& .MuiChip-icon': { color: '#fff' },
                          }}
                        />
                      )}
                    </Stack>

                    <Typography variant="caption" color="text.secondary">
                      {totalCards} card{totalCards !== 1 ? 's' : ''} &middot;{' '}
                      {boardLane.columns.length} column{boardLane.columns.length !== 1 ? 's' : ''}
                    </Typography>

                    <Stack direction="row" spacing={0.75} flexWrap="wrap" useFlexGap>
                      {boardLane.columns.map((colLane) => (
                        <Chip
                          key={colLane.column.id}
                          label={`${colLane.column.name}: ${colLane.cardCount}`}
                          size="small"
                          sx={{
                            height: 24,
                            fontSize: 12,
                            fontWeight: 500,
                            bgcolor: 'action.selected',
                            color: 'text.primary',
                          }}
                        />
                      ))}
                    </Stack>
                  </Stack>
                </CardContent>
              </CardActionArea>
            </Card>
          )
        })}
      </Box>
    </Box>
  )
}
