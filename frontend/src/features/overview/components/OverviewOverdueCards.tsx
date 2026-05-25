import { useMemo, useState } from 'react'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import MenuItem from '@mui/material/MenuItem'
import Select from '@mui/material/Select'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import WarningAmberOutlinedIcon from '@mui/icons-material/WarningAmberOutlined'
import ViewKanbanOutlinedIcon from '@mui/icons-material/ViewKanbanOutlined'
import ViewColumnOutlinedIcon from '@mui/icons-material/ViewColumnOutlined'
import { alpha } from '@mui/material/styles'
import ExpandLessIcon from '@mui/icons-material/ExpandLess'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import type { SelectChangeEvent } from '@mui/material/Select'
import type { FlatCard } from '../utils/overviewUtils'
import { formatDueDate } from '../utils/overviewUtils'

interface OverviewOverdueCardsProps {
  cards: FlatCard[]
  onCardClick?: (card: FlatCard) => void
}

const PAGE_SIZE = 5

export function OverviewOverdueCards({ cards, onCardClick }: OverviewOverdueCardsProps) {
  const [showCount, setShowCount] = useState(PAGE_SIZE)
  const [boardFilter, setBoardFilter] = useState<string | null>(null)

  const boardNames = useMemo(
    () => [...new Set(cards.map((c) => c.boardName))].sort(),
    [cards],
  )

  const filtered = useMemo(
    () => (boardFilter ? cards.filter((c) => c.boardName === boardFilter) : cards),
    [cards, boardFilter],
  )

  if (cards.length === 0) return null

  const visible = filtered.slice(0, showCount)
  const remaining = filtered.length - showCount
  const hasMore = remaining > 0

  return (
    <Box>
      <Stack
        direction="row"
        spacing={1.5}
        useFlexGap
        alignItems="center"
        sx={{ mb: 2.5, flexWrap: 'wrap', rowGap: 1 }}
      >
        <WarningAmberOutlinedIcon sx={{ fontSize: 22, color: 'error.main' }} />
        <Typography variant="h3" sx={{ fontWeight: 700 }}>
          Overdue
        </Typography>
        <Chip
          label={filtered.length}
          size="small"
          sx={{
            height: 24,
            fontSize: 12,
            fontWeight: 700,
            bgcolor: 'error.main',
            color: '#fff',
          }}
        />
        {boardNames.length >= 1 && (
          <Select
            size="small"
            value={boardFilter ?? '__all__'}
            onChange={(e: SelectChangeEvent) => {
              setBoardFilter(e.target.value === '__all__' ? null : e.target.value)
              setShowCount(PAGE_SIZE)
            }}
            sx={{
              ml: { xs: 0, sm: 'auto' },
              width: { xs: '100%', sm: 'auto' },
              minWidth: { xs: 0, sm: 100 },
              maxWidth: { xs: '100%', sm: 180 },
              height: 32,
              fontSize: 13,
              '& .MuiSelect-select': { overflow: 'hidden', textOverflow: 'ellipsis' },
            }}
          >
            <MenuItem value="__all__">All boards</MenuItem>
            {boardNames.map((name) => (
              <MenuItem key={name} value={name}>{name}</MenuItem>
            ))}
          </Select>
        )}
      </Stack>

      <Stack spacing={1}>
        {visible.map((card) => (
          <Box
            key={card.id}
            onClick={onCardClick ? () => onCardClick(card) : undefined}
            sx={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: 2,
              px: 2,
              py: 1.5,
              borderRadius: 2,
              border: '1px solid',
              borderColor: (theme) =>
                theme.palette.mode === 'dark'
                  ? 'rgba(239,68,68,0.25)'
                  : 'rgba(220,38,38,0.2)',
              bgcolor: (theme) =>
                theme.palette.mode === 'dark'
                  ? 'rgba(239,68,68,0.06)'
                  : 'rgba(220,38,38,0.04)',
              ...(onCardClick && {
                cursor: 'pointer',
                '&:hover': { opacity: 0.8 },
              }),
            }}
          >
            <Box sx={{ minWidth: 0, flex: 1, overflow: 'hidden' }}>
              <Typography
                variant="body2"
                sx={{ fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
              >
                {card.title}
              </Typography>
              <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', columnGap: 0.5, rowGap: 0.5, mt: 0.25 }}>
                <Chip
                  icon={<ViewKanbanOutlinedIcon sx={{ fontSize: 12 }} />}
                  label={card.boardName}
                  size="small"
                  sx={{
                    height: 18,
                    fontSize: 10,
                    fontWeight: 600,
                    maxWidth: '100%',
                    bgcolor: (theme) => alpha(theme.palette.warning.main, 0.12),
                    color: 'warning.main',
                    '& .MuiChip-label': { px: 0.5 },
                    '& .MuiChip-icon': { color: 'warning.main', ml: 0.5, mr: -0.25 },
                  }}
                />
                <Chip
                  icon={<ViewColumnOutlinedIcon sx={{ fontSize: 12 }} />}
                  label={card.columnName}
                  size="small"
                  sx={{
                    height: 18,
                    fontSize: 10,
                    fontWeight: 600,
                    maxWidth: '100%',
                    bgcolor: (theme) => alpha(theme.palette.primary.main, 0.12),
                    color: 'primary.main',
                    '& .MuiChip-label': { px: 0.5 },
                    '& .MuiChip-icon': { color: 'primary.main', ml: 0.5, mr: -0.25 },
                  }}
                />
              </Box>
            </Box>
            <Typography
              variant="caption"
              sx={{ fontWeight: 600, color: 'error.main', whiteSpace: 'nowrap', flexShrink: 0 }}
            >
              {formatDueDate(card.dueDate!)}
            </Typography>
          </Box>
        ))}
      </Stack>

      {(hasMore || showCount > PAGE_SIZE) && (
        <Stack direction="row" spacing={1} sx={{ mt: 1.5 }}>
          {hasMore && (
            <Button
              size="small"
              onClick={() => setShowCount((v) => v + PAGE_SIZE)}
              endIcon={<ExpandMoreIcon />}
              sx={{ color: 'text.secondary' }}
            >
              {remaining <= PAGE_SIZE
                ? `Show ${remaining} more`
                : `Show ${PAGE_SIZE} more (${remaining - PAGE_SIZE} others)`}
            </Button>
          )}
          {showCount > PAGE_SIZE && (
            <Button
              size="small"
              onClick={() => setShowCount(PAGE_SIZE)}
              endIcon={<ExpandLessIcon />}
              sx={{ color: 'text.secondary' }}
            >
              Show less
            </Button>
          )}
        </Stack>
      )}
    </Box>
  )
}
