import { useMemo, useState } from 'react'
import Avatar from '@mui/material/Avatar'
import { UserAvatar } from '@/features/auth'
import Box from '@mui/material/Box'
import MenuItem from '@mui/material/MenuItem'
import Select from '@mui/material/Select'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import GroupOutlinedIcon from '@mui/icons-material/GroupOutlined'
import type { SelectChangeEvent } from '@mui/material/Select'
import { getTeamWorkload, getUnassignedTotals } from '../utils/overviewUtils'
import type { FlatCard } from '../utils/overviewUtils'

type WorkloadMetric = 'tasks' | 'estimated' | 'logged'

interface OverviewTeamWorkloadProps {
  cards: FlatCard[]
}

const METRIC_OPTIONS: { value: WorkloadMetric; label: string }[] = [
  { value: 'tasks', label: 'Tasks' },
  { value: 'estimated', label: 'Estimated hours' },
  { value: 'logged', label: 'Logged hours' },
]

function formatHours(hours: number): string {
  if (hours === 0) return '0'
  if (hours < 10) return hours.toFixed(1).replace(/\.0$/, '')
  return Math.round(hours).toString()
}

export function OverviewTeamWorkload({ cards }: OverviewTeamWorkloadProps) {
  const [boardFilter, setBoardFilter] = useState<string | null>(null)
  const [metric, setMetric] = useState<WorkloadMetric>('tasks')

  const boardNames = useMemo(
    () => [...new Set(cards.map((c) => c.boardName))].sort(),
    [cards],
  )

  const filteredCards = useMemo(
    () => (boardFilter ? cards.filter((c) => c.boardName === boardFilter) : cards),
    [cards, boardFilter],
  )

  const members = useMemo(() => getTeamWorkload(filteredCards), [filteredCards])
  const unassigned = useMemo(() => getUnassignedTotals(filteredCards), [filteredCards])

  const getMemberValue = (m: (typeof members)[number]): number => {
    if (metric === 'tasks') return m.cardCount
    if (metric === 'estimated') return m.estimatedHours
    return m.loggedHours
  }

  const unassignedValue =
    metric === 'tasks'
      ? unassigned.cardCount
      : metric === 'estimated'
        ? unassigned.estimatedHours
        : unassigned.loggedHours

  const sortedMembers = useMemo(
    () => [...members].sort((a, b) => getMemberValue(b) - getMemberValue(a)),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [members, metric],
  )

  const maxValue = Math.max(
    ...sortedMembers.map((m) => getMemberValue(m)),
    unassignedValue,
    1,
  )

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
        <GroupOutlinedIcon sx={{ fontSize: 22, color: 'primary.main', flexShrink: 0 }} />
        <Typography
          variant="h3"
          sx={{ fontWeight: 700, whiteSpace: 'nowrap', flexShrink: 0 }}
        >
          Team Workload
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
            onChange={(e: SelectChangeEvent) => setMetric(e.target.value as WorkloadMetric)}
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

      <Stack spacing={1.5}>
        {sortedMembers.map((member) => {
          const displayName = member.userName || member.email || 'Unknown'
          const value = getMemberValue(member)

          return (
            <Stack key={member.userId} direction="row" spacing={1.5} alignItems="center">
              <UserAvatar
                  userId={member.userId}
                  name={displayName}
                  sx={{
                    width: 30,
                    height: 30,
                    fontSize: 13,
                    fontWeight: 600,
                    bgcolor: 'primary.main',
                    color: 'primary.contrastText',
                    flexShrink: 0,
                  }}
                />
                <Typography
                  variant="body2"
                  sx={{ fontWeight: 600, flex: 1, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
                >
                  {displayName}
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
                        bgcolor: 'primary.main',
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

        <Stack direction="row" spacing={1.5} alignItems="center">
          <Avatar
            sx={{
              width: 30,
              height: 30,
              fontSize: 13,
              fontWeight: 600,
              bgcolor: 'action.selected',
              color: 'text.secondary',
              flexShrink: 0,
            }}
          >
            ?
          </Avatar>
          <Typography
            variant="body2"
            sx={{ fontWeight: 600, flex: 1, minWidth: 0, color: 'text.secondary', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
          >
            Unassigned
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
                  width: `${(unassignedValue / maxValue) * 100}%`,
                  height: '100%',
                  borderRadius: 1,
                  bgcolor: 'text.disabled',
                }}
              />
            </Box>
            <Typography
              variant="caption"
              sx={{ fontWeight: 700, minWidth: 32, textAlign: 'right' }}
            >
              {formatValue(unassignedValue)}
            </Typography>
          </Box>
        </Stack>
      </Stack>
    </Box>
  )
}
