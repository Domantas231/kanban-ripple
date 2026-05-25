import FilterListIcon from '@mui/icons-material/FilterList'
import Badge from '@mui/material/Badge'
import Drawer from '@mui/material/Drawer'
import IconButton from '@mui/material/IconButton'
import Tooltip from '@mui/material/Tooltip'
import { useMemo, useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import { useProjectMembers } from '@/features/projects'
import { useBoardTags } from '@/features/cards'
import type { Guid } from '@/lib/types'
import {
  countActiveClientFilters,
  parseClientFiltersFromSearch,
  serializeClientFiltersToSearch,
  type ClientFilterSearchParams,
} from '@/features/cards'
import { FilterPanel } from './FilterPanel'

type BoardFilterControlProps = {
  projectId: Guid
  boardId: Guid
  cardId?: Guid
  tagIds?: string
  userIds?: string
  clientFilterSearch: ClientFilterSearchParams
}

export function BoardFilterControl({
  projectId,
  boardId,
  cardId,
  tagIds,
  userIds,
  clientFilterSearch,
}: BoardFilterControlProps) {
  const navigate = useNavigate()

  const tagsQuery = useBoardTags(boardId)
  const membersQuery = useProjectMembers(projectId)

  const tags = tagsQuery.data ?? []
  const members = membersQuery.data ?? []

  const [drawerOpen, setDrawerOpen] = useState(false)

  const value = useMemo(
    () => ({
      tagIds: parseCsvGuidList(tagIds),
      userIds: parseCsvGuidList(userIds),
      client: parseClientFiltersFromSearch(clientFilterSearch),
    }),
    [tagIds, userIds, clientFilterSearch],
  )

  const activeFilterCount =
    value.tagIds.length + value.userIds.length + countActiveClientFilters(value.client)

  return (
    <>
      <Tooltip title="Filters">
        <IconButton
          color="inherit"
          onClick={() => setDrawerOpen(true)}
          aria-label="Open filters"
          size="small"
        >
          <Badge badgeContent={activeFilterCount} color="primary" max={99}>
            <FilterListIcon fontSize="small" />
          </Badge>
        </IconButton>
      </Tooltip>

      <Drawer
        anchor="right"
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
      >
        <FilterPanel
          tags={tags}
          members={members}
          value={value}
          disabled={tagsQuery.isLoading || membersQuery.isLoading}
          onApply={(nextValue) => {
            void navigate({
              to: '/projects/$projectId/boards/$boardId',
              params: {
                projectId,
                boardId,
              },
              search: {
                cardId,
                tagIds: toCsv(nextValue.tagIds),
                userIds: toCsv(nextValue.userIds),
                ...serializeClientFiltersToSearch(nextValue.client),
              },
            })
            setDrawerOpen(false)
          }}
          onClearAll={() => {
            void navigate({
              to: '/projects/$projectId/boards/$boardId',
              params: {
                projectId,
                boardId,
              },
              search: {
                cardId,
              },
            })
            setDrawerOpen(false)
          }}
          onClose={() => setDrawerOpen(false)}
        />
      </Drawer>
    </>
  )
}

function parseCsvGuidList(value: string | undefined): Guid[] {
  if (!value) {
    return []
  }

  return value
    .split(',')
    .map((item) => item.trim())
    .filter((item) => item.length > 0)
}

function toCsv(values: Guid[]): string | undefined {
  if (values.length === 0) {
    return undefined
  }

  return values.join(',')
}
