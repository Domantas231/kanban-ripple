import { useMemo, useState } from 'react'
import Box from '@mui/material/Box'
import MenuItem from '@mui/material/MenuItem'
import Select from '@mui/material/Select'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import LabelOutlinedIcon from '@mui/icons-material/LabelOutlined'
import type { SelectChangeEvent } from '@mui/material/Select'
import { getTagCounts } from '../utils/overviewUtils'
import type { FlatCard } from '../utils/overviewUtils'

type TagsMetric = 'tasks' | 'estimated' | 'logged'

interface OverviewTagsBreakdownProps {
  cards: FlatCard[]
}

const METRIC_OPTIONS: { value: TagsMetric; label: string }[] = [
  { value: 'tasks', label: 'Tasks' },
  { value: 'estimated', label: 'Estimated hours' },
  { value: 'logged', label: 'Logged hours' },
]

function formatHours(hours: number): string {
  if (hours === 0) return '0'
  if (hours < 10) return hours.toFixed(1).replace(/\.0$/, '')
  return Math.round(hours).toString()
}

export function OverviewTagsBreakdown({ cards }: OverviewTagsBreakdownProps) {
  const [boardFilter, setBoardFilter] = useState<string | null>(null)
  const [metric, setMetric] = useState<TagsMetric>('tasks')

  const boardNames = useMemo(
    () => [...new Set(cards.map((c) => c.boardName))].sort(),
    [cards],
  )

  const filteredCards = useMemo(
    () => (boardFilter ? cards.filter((c) => c.boardName === boardFilter) : cards),
    [cards, boardFilter],
  )

  const tags = useMemo(() => getTagCounts(filteredCards), [filteredCards])
  const hasAnyTags = useMemo(() => getTagCounts(cards).length > 0, [cards])

  const getTagValue = (t: (typeof tags)[number]): number => {
    if (metric === 'tasks') return t.count
    if (metric === 'estimated') return t.estimatedHours
    return t.loggedHours
  }

  const sortedTags = useMemo(
    () => [...tags].sort((a, b) => getTagValue(b) - getTagValue(a)),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [tags, metric],
  )

  if (!hasAnyTags) return null

  const maxValue = Math.max(...sortedTags.map((t) => getTagValue(t)), 1)

  const formatValue = (value: number): string =>
    metric === 'tasks' ? value.toString() : `${formatHours(value)}h`

  return (
    <Box>
      <Stack
        direction="row"
        spacing={1.5}
        useFlexGap
        alignItems="center"
        sx={{ mb: 2.5, flexWrap: 'wrap', rowGap: 1 }}
      >
        <LabelOutlinedIcon sx={{ fontSize: 20, color: 'primary.main', flexShrink: 0 }} />
        <Typography
          variant="h3"
          sx={{ fontWeight: 700, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
        >
          Tags
        </Typography>
        <Stack
          direction="row"
          spacing={1}
          useFlexGap
          alignItems="center"
          sx={{
            ml: { xs: 0, sm: 'auto' },
            width: { xs: '100%', sm: 'auto' },
            minWidth: 0,
            flexWrap: 'nowrap',
          }}
        >
          <Select
            size="small"
            value={metric}
            onChange={(e: SelectChangeEvent) => setMetric(e.target.value as TagsMetric)}
            sx={{
              width: { xs: '50%', sm: 120 },
              minWidth: 0,
              height: 32,
              fontSize: 13,
              flexShrink: 0,
            }}
          >
            {METRIC_OPTIONS.map((opt) => (
              <MenuItem key={opt.value} value={opt.value}>{opt.label}</MenuItem>
            ))}
          </Select>
          {boardNames.length >= 1 && (
            <Select
              size="small"
              value={boardFilter ?? '__all__'}
              onChange={(e: SelectChangeEvent) => {
                setBoardFilter(e.target.value === '__all__' ? null : e.target.value)
              }}
              sx={{
                minWidth: 0,
                flex: 1,
                maxWidth: { xs: '100%', sm: 180 },
                height: 32,
                fontSize: 13,
                '& .MuiSelect-select': { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
              }}
            >
              <MenuItem value="__all__">All boards</MenuItem>
              {boardNames.map((name) => (
                <MenuItem
                  key={name}
                  value={name}
                  sx={{ maxWidth: 280, display: 'block', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
                >
                  {name}
                </MenuItem>
              ))}
            </Select>
          )}
        </Stack>
      </Stack>

      {sortedTags.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          No tags on this board.
        </Typography>
      ) : (
        <Stack spacing={1.25}>
          {sortedTags.map((tag) => {
            const value = getTagValue(tag)
            return (
              <Stack key={tag.tagId} direction="row" spacing={1.5} alignItems="center">
                <Box
                  sx={{
                    width: 12,
                    height: 12,
                    borderRadius: '50%',
                    bgcolor: tag.tagColor || 'primary.main',
                    flexShrink: 0,
                  }}
                />
                <Typography
                  variant="body2"
                  sx={{ fontWeight: 600, flex: 1, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
                >
                  {tag.tagName}
                </Typography>
                <Box sx={{ flex: 1, minWidth: 0, display: 'flex', alignItems: 'center', gap: 1.5 }}>
                  <Box
                    sx={{
                      flex: 1,
                      height: 8,
                      borderRadius: 1,
                      bgcolor: 'action.selected',
                      overflow: 'hidden',
                    }}
                  >
                    <Box
                      sx={{
                        width: `${(value / maxValue) * 100}%`,
                        height: '100%',
                        borderRadius: 1,
                        bgcolor: tag.tagColor || 'primary.main',
                        opacity: 0.8,
                      }}
                    />
                  </Box>
                  <Typography
                    variant="caption"
                    sx={{ fontWeight: 700, minWidth: 32, textAlign: 'right' }}
                  >
                    {formatValue(value)}
                  </Typography>
                </Box>
              </Stack>
            )
          })}
        </Stack>
      )}
    </Box>
  )
}
