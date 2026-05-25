import CloseIcon from '@mui/icons-material/Close'
import { UserAvatar } from '@/features/auth'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import Divider from '@mui/material/Divider'
import IconButton from '@mui/material/IconButton'
import Slider from '@mui/material/Slider'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { alpha, useTheme } from '@mui/material/styles'
import { useMemo, useState } from 'react'
import type { Guid, ProjectMember, Tag } from '@/lib/types'
import type {
  ActivityFilter,
  AssigneeStateFilter,
  ClientCardFilters,
  DueDateFilter,
} from '@/features/cards'
import { EMPTY_CLIENT_FILTERS, countActiveClientFilters } from '@/features/cards'

type FilterPanelValue = {
  tagIds: Guid[]
  userIds: Guid[]
  client: ClientCardFilters
}

type FilterPanelProps = {
  tags: Tag[]
  members: ProjectMember[]
  value: FilterPanelValue
  onApply: (value: FilterPanelValue) => void
  onClearAll: () => void
  onClose: () => void
  disabled?: boolean
}

const DUE_OPTIONS: { value: DueDateFilter; label: string }[] = [
  { value: 'overdue', label: 'Overdue' },
  { value: 'today', label: 'Today' },
  { value: 'week', label: 'This week' },
  { value: 'none', label: 'No due date' },
]

const ASSIGNEE_OPTIONS: { value: AssigneeStateFilter; label: string }[] = [
  { value: 'me', label: 'Assigned to me' },
  { value: 'unassigned', label: 'Unassigned' },
  { value: 'multiple', label: 'Multiple assignees' },
]

const ACTIVITY_OPTIONS: { value: ActivityFilter; label: string }[] = [
  { value: '24h', label: 'Last 24h' },
  { value: '7d', label: 'Last 7 days' },
  { value: '30d', label: 'Last 30 days' },
  { value: 'stale', label: 'Stale (30d+)' },
]

const HOURS_MIN = 0
const HOURS_MAX = 80
const HOURS_STEP = 0.5

export function FilterPanel({ tags, members, value, onApply, onClearAll, onClose, disabled = false }: FilterPanelProps) {
  const [draft, setDraft] = useState<FilterPanelValue>(value)
  const theme = useTheme()

  const sortedMembers = useMemo(
    () =>
      [...members].sort((a, b) => {
        const left = getMemberLabel(a)
        const right = getMemberLabel(b)
        return left.localeCompare(right)
      }),
    [members],
  )

  const clientFilterCount = countActiveClientFilters(draft.client)
  const activeFilterCount = draft.tagIds.length + draft.userIds.length + clientFilterCount
  const hasActiveFilters = activeFilterCount > 0

  const toggleTag = (tagId: Guid) => {
    setDraft((previous) => ({
      ...previous,
      tagIds: previous.tagIds.includes(tagId)
        ? previous.tagIds.filter((id) => id !== tagId)
        : [...previous.tagIds, tagId],
    }))
  }

  const toggleUser = (userId: Guid) => {
    setDraft((previous) => ({
      ...previous,
      userIds: previous.userIds.includes(userId)
        ? previous.userIds.filter((id) => id !== userId)
        : [...previous.userIds, userId],
    }))
  }

  const toggleCreator = (userId: Guid) => {
    setDraft((previous) => ({
      ...previous,
      client: {
        ...previous.client,
        createdByIds: previous.client.createdByIds.includes(userId)
          ? previous.client.createdByIds.filter((id) => id !== userId)
          : [...previous.client.createdByIds, userId],
      },
    }))
  }

  const setDueDate = (next: DueDateFilter | null) => {
    setDraft((previous) => ({ ...previous, client: { ...previous.client, dueDate: next } }))
  }

  const setAssigneeState = (next: AssigneeStateFilter | null) => {
    setDraft((previous) => ({ ...previous, client: { ...previous.client, assigneeState: next } }))
  }

  const setActivity = (next: ActivityFilter | null) => {
    setDraft((previous) => ({ ...previous, client: { ...previous.client, activity: next } }))
  }

  const setHasAttachments = (next: boolean | null) => {
    setDraft((previous) => ({ ...previous, client: { ...previous.client, hasAttachments: next } }))
  }

  const setHasComments = (next: boolean | null) => {
    setDraft((previous) => ({ ...previous, client: { ...previous.client, hasComments: next } }))
  }

  const estRangeActive = draft.client.estMin !== null || draft.client.estMax !== null
  const estSliderValue: [number, number] = [
    draft.client.estMin ?? HOURS_MIN,
    draft.client.estMax ?? HOURS_MAX,
  ]

  const setEstRange = (next: [number, number] | null) => {
    setDraft((previous) => ({
      ...previous,
      client: {
        ...previous.client,
        estMin: next ? next[0] : null,
        estMax: next ? next[1] : null,
      },
    }))
  }

  return (
    <Box sx={{ width: 340, height: '100%', display: 'flex', flexDirection: 'column' }}>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ px: 2.5, py: 2 }}>
        <Stack direction="row" spacing={1} alignItems="center">
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            Filters
          </Typography>
          {hasActiveFilters ? (
            <Box
              sx={{
                bgcolor: 'primary.main',
                color: 'primary.contrastText',
                borderRadius: '50%',
                width: 22,
                height: 22,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontSize: '0.75rem',
                fontWeight: 700,
              }}
            >
              {activeFilterCount}
            </Box>
          ) : null}
        </Stack>
        <IconButton size="small" onClick={onClose} aria-label="Close filter panel">
          <CloseIcon fontSize="small" />
        </IconButton>
      </Stack>

      <Divider />

      <Box sx={{ flex: 1, overflowY: 'auto', px: 2.5, py: 2 }}>
        <Stack spacing={3}>
          {tags.length > 0 ? (
            <Section title="Tags">
              <Stack spacing={0.25}>
                {tags.map((tag) => {
                  const isSelected = draft.tagIds.includes(tag.id)
                  const tagColor = tag.color?.trim() || '#9e9e9e'
                  return (
                    <Stack
                      key={tag.id}
                      direction="row"
                      alignItems="center"
                      spacing={1}
                      onClick={() => !disabled && toggleTag(tag.id)}
                      sx={{
                        py: 0.5,
                        px: 0.5,
                        borderRadius: 1,
                        cursor: disabled ? 'default' : 'pointer',
                        '&:hover': disabled ? {} : { bgcolor: 'action.hover' },
                      }}
                    >
                      <Checkbox checked={isSelected} disabled={disabled} size="small" sx={{ p: 0.25 }} tabIndex={-1} />
                      <Box
                        sx={{
                          width: 12,
                          height: 12,
                          borderRadius: '50%',
                          bgcolor: tagColor,
                          flexShrink: 0,
                          border: `1px solid ${alpha(tagColor, 0.5)}`,
                        }}
                      />
                      <Typography variant="body2" noWrap sx={{ flex: 1 }}>
                        {tag.name}
                      </Typography>
                    </Stack>
                  )
                })}
              </Stack>
            </Section>
          ) : null}

          {sortedMembers.length > 0 ? (
            <Section title="Assignees">
              <Stack spacing={0.25}>
                {sortedMembers.map((member) => {
                  const isSelected = draft.userIds.includes(member.userId)
                  const label = getMemberLabel(member)
                  return (
                    <Stack
                      key={member.userId}
                      direction="row"
                      alignItems="center"
                      spacing={1}
                      onClick={() => !disabled && toggleUser(member.userId)}
                      sx={{
                        py: 0.5,
                        px: 0.5,
                        borderRadius: 1,
                        cursor: disabled ? 'default' : 'pointer',
                        '&:hover': disabled ? {} : { bgcolor: 'action.hover' },
                      }}
                    >
                      <Checkbox checked={isSelected} disabled={disabled} size="small" sx={{ p: 0.25 }} tabIndex={-1} />
                      <UserAvatar
                        userId={member.userId}
                        name={label}
                        sx={{
                          width: 24,
                          height: 24,
                          fontSize: '0.75rem',
                          bgcolor: theme.palette.primary.main,
                          flexShrink: 0,
                        }}
                      />
                      <Typography variant="body2" noWrap sx={{ flex: 1 }}>
                        {label}
                      </Typography>
                    </Stack>
                  )
                })}
              </Stack>
            </Section>
          ) : null}

          <Section title="Due date">
            <ChoiceChips<DueDateFilter>
              options={DUE_OPTIONS}
              value={draft.client.dueDate}
              onChange={setDueDate}
              disabled={disabled}
            />
          </Section>

          <Section title="Assignee state">
            <ChoiceChips<AssigneeStateFilter>
              options={ASSIGNEE_OPTIONS}
              value={draft.client.assigneeState}
              onChange={setAssigneeState}
              disabled={disabled}
            />
          </Section>

          <Section title="Activity">
            <ChoiceChips<ActivityFilter>
              options={ACTIVITY_OPTIONS}
              value={draft.client.activity}
              onChange={setActivity}
              disabled={disabled}
            />
          </Section>

          {sortedMembers.length > 0 ? (
            <Section title="Created by">
              <Stack spacing={0.25}>
                {sortedMembers.map((member) => {
                  const isSelected = draft.client.createdByIds.includes(member.userId)
                  const label = getMemberLabel(member)
                  return (
                    <Stack
                      key={member.userId}
                      direction="row"
                      alignItems="center"
                      spacing={1}
                      onClick={() => !disabled && toggleCreator(member.userId)}
                      sx={{
                        py: 0.5,
                        px: 0.5,
                        borderRadius: 1,
                        cursor: disabled ? 'default' : 'pointer',
                        '&:hover': disabled ? {} : { bgcolor: 'action.hover' },
                      }}
                    >
                      <Checkbox checked={isSelected} disabled={disabled} size="small" sx={{ p: 0.25 }} tabIndex={-1} />
                      <UserAvatar
                        userId={member.userId}
                        name={label}
                        sx={{
                          width: 24,
                          height: 24,
                          fontSize: '0.75rem',
                          bgcolor: theme.palette.primary.main,
                          flexShrink: 0,
                        }}
                      />
                      <Typography variant="body2" noWrap sx={{ flex: 1 }}>
                        {label}
                      </Typography>
                    </Stack>
                  )
                })}
              </Stack>
            </Section>
          ) : null}

          <Section title="Attachments">
            <YesNoChips value={draft.client.hasAttachments} onChange={setHasAttachments} disabled={disabled} />
          </Section>

          <Section title="Comments">
            <YesNoChips value={draft.client.hasComments} onChange={setHasComments} disabled={disabled} />
          </Section>

          <Section title="Estimated hours">
            <EstimatedHoursSection
              active={estRangeActive}
              value={estSliderValue}
              onChange={setEstRange}
              disabled={disabled}
            />
          </Section>
        </Stack>
      </Box>

      <Divider />
      <Stack direction="row" spacing={1.5} sx={{ px: 2.5, py: 2 }} justifyContent="space-between">
        <Button
          size="small"
          onClick={() => {
            setDraft({ tagIds: [], userIds: [], client: EMPTY_CLIENT_FILTERS })
            onClearAll()
          }}
          disabled={disabled || !hasActiveFilters}
          sx={{ textTransform: 'none' }}
        >
          Clear all
        </Button>
        <Button
          variant="contained"
          size="small"
          onClick={() => onApply(draft)}
          disabled={disabled}
          sx={{ textTransform: 'none', fontWeight: 600 }}
        >
          Apply filters
        </Button>
      </Stack>
    </Box>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Box>
      <Typography
        variant="caption"
        sx={{
          fontWeight: 600,
          textTransform: 'uppercase',
          letterSpacing: '0.08em',
          color: 'text.secondary',
          mb: 1,
          display: 'block',
        }}
      >
        {title}
      </Typography>
      {children}
    </Box>
  )
}

type ChoiceChipsProps<T extends string> = {
  options: { value: T; label: string }[]
  value: T | null
  onChange: (next: T | null) => void
  disabled: boolean
}

function ChoiceChips<T extends string>({ options, value, onChange, disabled }: ChoiceChipsProps<T>) {
  return (
    <Stack direction="row" spacing={0.75} useFlexGap flexWrap="wrap">
      {options.map((option) => {
        const isSelected = option.value === value
        return (
          <Chip
            key={option.value}
            label={option.label}
            size="small"
            disabled={disabled}
            variant={isSelected ? 'filled' : 'outlined'}
            color={isSelected ? 'primary' : 'default'}
            onClick={() => onChange(isSelected ? null : option.value)}
            sx={{ cursor: disabled ? 'default' : 'pointer' }}
          />
        )
      })}
    </Stack>
  )
}

function YesNoChips({
  value,
  onChange,
  disabled,
}: {
  value: boolean | null
  onChange: (next: boolean | null) => void
  disabled: boolean
}) {
  return (
    <Stack direction="row" spacing={0.75} useFlexGap flexWrap="wrap">
      {[
        { label: 'Has', next: true as const },
        { label: 'None', next: false as const },
      ].map(({ label, next }) => {
        const isSelected = value === next
        return (
          <Chip
            key={label}
            label={label}
            size="small"
            disabled={disabled}
            variant={isSelected ? 'filled' : 'outlined'}
            color={isSelected ? 'primary' : 'default'}
            onClick={() => onChange(isSelected ? null : next)}
            sx={{ cursor: disabled ? 'default' : 'pointer' }}
          />
        )
      })}
    </Stack>
  )
}

function EstimatedHoursSection({
  active,
  value,
  onChange,
  disabled,
}: {
  active: boolean
  value: [number, number]
  onChange: (next: [number, number] | null) => void
  disabled: boolean
}) {
  return (
    <Stack spacing={1.5}>
      <Stack direction="row" spacing={0.75} alignItems="center">
        <Chip
          label={active ? 'Active' : 'Any'}
          size="small"
          disabled={disabled}
          variant={active ? 'filled' : 'outlined'}
          color={active ? 'primary' : 'default'}
          onClick={() => onChange(active ? null : [HOURS_MIN, HOURS_MAX])}
          sx={{ cursor: disabled ? 'default' : 'pointer' }}
        />
        <Typography variant="caption" color="text.secondary">
          {active ? `${formatHours(value[0])} – ${formatHours(value[1])} h` : 'All tasks'}
        </Typography>
      </Stack>

      <Box sx={{ px: 1, opacity: active ? 1 : 0.5, pointerEvents: active && !disabled ? 'auto' : 'none' }}>
        <Slider
          value={value}
          onChange={(_, next) => {
            if (Array.isArray(next)) {
              onChange([next[0], next[1]])
            }
          }}
          disabled={disabled || !active}
          min={HOURS_MIN}
          max={HOURS_MAX}
          step={HOURS_STEP}
          size="small"
          valueLabelDisplay="auto"
          valueLabelFormat={(v) => `${formatHours(v)} h`}
        />
      </Box>

      {active ? (
        <Stack direction="row" spacing={1}>
          <TextField
            label="Min"
            type="number"
            size="small"
            value={value[0]}
            disabled={disabled}
            inputProps={{ min: HOURS_MIN, max: value[1], step: HOURS_STEP }}
            onChange={(event) => {
              const parsed = Number(event.target.value)
              if (Number.isNaN(parsed)) return
              const next = Math.max(HOURS_MIN, Math.min(parsed, value[1]))
              onChange([next, value[1]])
            }}
          />
          <TextField
            label="Max"
            type="number"
            size="small"
            value={value[1]}
            disabled={disabled}
            inputProps={{ min: value[0], step: HOURS_STEP }}
            onChange={(event) => {
              const parsed = Number(event.target.value)
              if (Number.isNaN(parsed)) return
              const next = Math.max(value[0], parsed)
              onChange([value[0], next])
            }}
          />
        </Stack>
      ) : null}
    </Stack>
  )
}

function formatHours(value: number): string {
  return Number.isInteger(value) ? value.toFixed(0) : value.toFixed(1)
}

function getMemberLabel(member: ProjectMember): string {
  return member.userName?.trim() || member.user?.userName?.trim() || member.email?.trim() || member.user?.email?.trim() || member.userId
}
