import { useDraggable } from '@dnd-kit/core'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import AssignmentOutlinedIcon from '@mui/icons-material/AssignmentOutlined'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import { useUiStore } from '@/stores/uiStore'
import type { UnscheduledCard } from '@/lib/types'

const SIDEBAR_WIDTH = 280
const COLLAPSED_WIDTH = 40

type PlannerSidebarProps = {
  cards: UnscheduledCard[]
  isLoading: boolean
}

type DraggableSidebarCardProps = {
  card: UnscheduledCard
}

function DraggableSidebarCard({ card }: DraggableSidebarCardProps) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `sidebar-${card.id}`,
    data: { type: 'sidebar-card', card },
  })

  return (
    <Box
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      sx={{
        p: 1.5,
        mb: 0.5,
        borderRadius: 1,
        border: 1,
        borderColor: 'divider',
        bgcolor: 'background.paper',
        cursor: 'grab',
        opacity: isDragging ? 0.5 : 1,
        userSelect: 'none',
        '&:hover': {
          bgcolor: 'action.hover',
        },
      }}
    >
      <Typography
        variant="body2"
        sx={{
          fontWeight: 500,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
        }}
      >
        {card.title}
      </Typography>
      <Box sx={{ display: 'flex', gap: 0.5, mt: 0.5 }}>
        <Chip
          label={card.boardName}
          size="small"
          variant="outlined"
          sx={{ height: 20, fontSize: '0.6875rem' }}
        />
        <Chip
          label={card.columnName}
          size="small"
          sx={{ height: 20, fontSize: '0.6875rem', bgcolor: 'action.selected' }}
        />
      </Box>
    </Box>
  )
}

export function PlannerSidebar({ cards, isLoading }: PlannerSidebarProps) {
  const collapsed = useUiStore((state) => state.plannerSidebarCollapsed)
  const toggleSidebar = useUiStore((state) => state.togglePlannerSidebar)

  if (collapsed) {
    return (
      <Box
        sx={{
          width: COLLAPSED_WIDTH,
          minWidth: COLLAPSED_WIDTH,
          borderRight: 1,
          borderColor: 'divider',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          pt: 1,
        }}
      >
        <Tooltip title={`Show unscheduled (${cards.length})`} placement="right">
          <IconButton size="small" onClick={toggleSidebar} aria-label="Expand sidebar">
            <ChevronRightIcon sx={{ fontSize: 18 }} />
          </IconButton>
        </Tooltip>
        {cards.length > 0 && (
          <Typography
            variant="caption"
            sx={{
              mt: 0.5,
              fontWeight: 600,
              fontSize: '0.6875rem',
              color: 'text.secondary',
              writingMode: 'vertical-rl',
              textOrientation: 'mixed',
            }}
          >
            {cards.length}
          </Typography>
        )}
      </Box>
    )
  }

  return (
    <Box
      sx={{
        width: SIDEBAR_WIDTH,
        minWidth: SIDEBAR_WIDTH,
        borderRight: 1,
        borderColor: 'divider',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
      }}
    >
      <Box sx={{ px: 2, py: 1.5, borderBottom: 1, borderColor: 'divider', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography variant="caption" sx={{ fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.5 }}>
          Unscheduled ({cards.length})
        </Typography>
        <Tooltip title="Collapse sidebar">
          <IconButton size="small" onClick={toggleSidebar} aria-label="Collapse sidebar">
            <ChevronLeftIcon sx={{ fontSize: 18 }} />
          </IconButton>
        </Tooltip>
      </Box>

      <Box
        sx={{
          flex: 1,
          overflow: 'auto',
          px: 1,
          py: 1,
        }}
      >
        {isLoading ? (
          <Typography variant="body2" color="text.secondary" sx={{ p: 1 }}>
            Loading cards...
          </Typography>
        ) : cards.length === 0 ? (
          <Box sx={{ p: 2, textAlign: 'center' }}>
            <AssignmentOutlinedIcon sx={{ fontSize: 32, color: 'text.disabled', mb: 1 }} />
            <Typography variant="body2" color="text.secondary">
              No unscheduled cards
            </Typography>
            <Typography variant="caption" color="text.disabled">
              All cards for this day have been planned
            </Typography>
          </Box>
        ) : (
          cards.map((card) => (
            <DraggableSidebarCard key={card.id} card={card} />
          ))
        )}
      </Box>
    </Box>
  )
}

export { SIDEBAR_WIDTH }
