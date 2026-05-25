import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import IconButton from '@mui/material/IconButton'
import ListItemText from '@mui/material/ListItemText'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import Tooltip from '@mui/material/Tooltip'
import FilterListIcon from '@mui/icons-material/FilterList'
import type { Guid } from '@/lib/types'

export const ZOOM_LEVELS = [24, 32, 40, 48, 56] as const
export const DEFAULT_ZOOM_INDEX = 2

type BoardOption = {
  id: Guid
  name: string
}

type GanttToolbarProps = {
  onScrollToToday: () => void
  boards: BoardOption[]
  hiddenBoardIds: ReadonlySet<Guid>
  onToggleBoardFilter: (boardId: Guid) => void
}

export function GanttToolbar({
  onScrollToToday,
  boards,
  hiddenBoardIds,
  onToggleBoardFilter,
}: GanttToolbarProps) {
  const [filterAnchor, setFilterAnchor] = useState<HTMLElement | null>(null)
  const isFilterActive = hiddenBoardIds.size > 0

  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        px: 2,
        py: 1,
        borderBottom: 1,
        borderColor: 'divider',
        bgcolor: 'background.paper',
        gap: 1.5,
        flexShrink: 0,
        minHeight: 48,
      }}
    >
      <Button
        size="small"
        variant="outlined"
        onClick={onScrollToToday}
        sx={{ textTransform: 'none', fontWeight: 600, fontSize: '0.8125rem', px: 2 }}
      >
        Today
      </Button>

      <Box sx={{ flex: 1 }} />

      <Tooltip title="Filter by list">
        <IconButton
          size="small"
          aria-label="Filter by list"
          onClick={(event) => setFilterAnchor(event.currentTarget)}
          sx={{
            color: isFilterActive ? 'primary.main' : 'text.secondary',
          }}
        >
          <FilterListIcon fontSize="small" />
        </IconButton>
      </Tooltip>

      <Menu
        anchorEl={filterAnchor}
        open={Boolean(filterAnchor)}
        onClose={() => setFilterAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { minWidth: 220 } } }}
      >
        {boards.length === 0 ? (
          <MenuItem disabled>No lists</MenuItem>
        ) : (
          boards.map((board) => {
            const isVisible = !hiddenBoardIds.has(board.id)
            return (
              <MenuItem
                key={board.id}
                onClick={() => onToggleBoardFilter(board.id)}
                dense
              >
                <Checkbox edge="start" checked={isVisible} tabIndex={-1} disableRipple size="small" />
                <ListItemText primary={board.name} />
              </MenuItem>
            )
          })
        )}
      </Menu>
    </Box>
  )
}
