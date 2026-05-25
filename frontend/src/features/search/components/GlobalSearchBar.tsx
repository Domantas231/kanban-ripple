import { useNavigate } from '@tanstack/react-router'
import Box from '@mui/material/Box'
import Dialog from '@mui/material/Dialog'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import List from '@mui/material/List'
import ListItemButton from '@mui/material/ListItemButton'
import ListItemIcon from '@mui/material/ListItemIcon'
import ListItemText from '@mui/material/ListItemText'
import ListSubheader from '@mui/material/ListSubheader'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import useMediaQuery from '@mui/material/useMediaQuery'
import { useTheme } from '@mui/material/styles'
import CloseIcon from '@mui/icons-material/Close'
import AssignmentOutlinedIcon from '@mui/icons-material/AssignmentOutlined'
import ViewKanbanOutlinedIcon from '@mui/icons-material/ViewKanbanOutlined'
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined'
import HistoryIcon from '@mui/icons-material/History'
import SearchIcon from '@mui/icons-material/Search'
import ViewColumnOutlinedIcon from '@mui/icons-material/ViewColumnOutlined'
import { useCallback, useMemo, useRef, useState } from 'react'
import { useGlobalSearch } from '@/features/search/api/search'
import type { GlobalSearchItem } from '@/lib/types'

const TYPE_ORDER = ['project', 'board', 'column', 'card'] as const

const TYPE_LABELS: Record<string, string> = {
  project: 'Workspaces',
  board: 'Boards',
  column: 'Lists',
  card: 'Tasks',
}

const TYPE_ICONS: Record<string, React.ReactNode> = {
  project: <DashboardOutlinedIcon fontSize="small" />,
  board: <ViewKanbanOutlinedIcon fontSize="small" />,
  column: <ViewColumnOutlinedIcon fontSize="small" />,
  card: <AssignmentOutlinedIcon fontSize="small" />,
}

const RECENT_SEARCHES_KEY = 'kanban-recent-searches'
const MAX_RECENT_SEARCHES = 5

function getRecentSearches(): string[] {
  try {
    const stored = localStorage.getItem(RECENT_SEARCHES_KEY)
    return stored ? (JSON.parse(stored) as string[]) : []
  } catch {
    return []
  }
}

function addRecentSearch(query: string) {
  const trimmed = query.trim()
  if (!trimmed) return
  const recent = getRecentSearches().filter((s) => s !== trimmed)
  recent.unshift(trimmed)
  localStorage.setItem(RECENT_SEARCHES_KEY, JSON.stringify(recent.slice(0, MAX_RECENT_SEARCHES)))
}

function clearRecentSearches() {
  localStorage.removeItem(RECENT_SEARCHES_KEY)
}

export function GlobalSearchBar() {
  const navigate = useNavigate()
  const theme = useTheme()
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'))
  const inputRef = useRef<HTMLInputElement | null>(null)
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [selectedIndex, setSelectedIndex] = useState(-1)
  const [recentSearches, setRecentSearches] = useState<string[]>([])

  const searchQuery = useGlobalSearch(query)

  const hasQuery = query.trim().length > 0
  const isLoading = hasQuery && (searchQuery.isLoading || searchQuery.isFetching)
  const items = searchQuery.data?.items ?? []
  const noResults = hasQuery && !isLoading && items.length === 0

  const groupedItems = useMemo(() => {
    const groups: Record<string, GlobalSearchItem[]> = {}
    for (const item of items) {
      if (!groups[item.type]) {
        groups[item.type] = []
      }
      groups[item.type].push(item)
    }
    return groups
  }, [items])

  // Flat list of items for keyboard navigation
  const flatItems = useMemo(() => {
    const result: GlobalSearchItem[] = []
    for (const type of TYPE_ORDER) {
      const group = groupedItems[type]
      if (group && group.length > 0) {
        result.push(...group)
      }
    }
    return result
  }, [groupedItems])

  const [prevSelectionDeps, setPrevSelectionDeps] = useState({ count: flatItems.length, query })
  if (prevSelectionDeps.count !== flatItems.length || prevSelectionDeps.query !== query) {
    setPrevSelectionDeps({ count: flatItems.length, query })
    setSelectedIndex(-1)
  }

  const handleOpen = useCallback(() => {
    setRecentSearches(getRecentSearches())
    setOpen(true)
  }, [])

  const handleClose = useCallback(() => {
    setOpen(false)
    setQuery('')
    setSelectedIndex(-1)
  }, [])

  const handleSelect = useCallback(
    async (item: GlobalSearchItem) => {
      addRecentSearch(query)
      handleClose()

      const loc = item.location
      switch (item.type) {
        case 'project':
          await navigate({ to: '/projects/$projectId', params: { projectId: item.id } })
          break
        case 'board':
          if (loc?.projectId) {
            await navigate({
              to: '/projects/$projectId/boards/$boardId',
              params: { projectId: loc.projectId, boardId: item.id },
            })
          }
          break
        case 'column':
          if (loc?.projectId && loc?.boardId) {
            await navigate({
              to: '/projects/$projectId/boards/$boardId',
              params: { projectId: loc.projectId, boardId: loc.boardId },
            })
          }
          break
        case 'card':
          if (loc?.projectId && loc?.boardId) {
            await navigate({
              to: '/projects/$projectId/boards/$boardId',
              params: { projectId: loc.projectId, boardId: loc.boardId },
              search: { cardId: item.id },
            })
          }
          break
      }
    },
    [navigate, query, handleClose],
  )

  const handleKeyDown = useCallback(
    (event: React.KeyboardEvent) => {
      const totalItems = hasQuery ? flatItems.length : recentSearches.length

      switch (event.key) {
        case 'ArrowDown':
          event.preventDefault()
          setSelectedIndex((prev) => (prev < totalItems - 1 ? prev + 1 : 0))
          break
        case 'ArrowUp':
          event.preventDefault()
          setSelectedIndex((prev) => (prev > 0 ? prev - 1 : totalItems - 1))
          break
        case 'Enter':
          event.preventDefault()
          if (hasQuery && selectedIndex >= 0 && selectedIndex < flatItems.length) {
            void handleSelect(flatItems[selectedIndex])
          } else if (!hasQuery && selectedIndex >= 0 && selectedIndex < recentSearches.length) {
            setQuery(recentSearches[selectedIndex])
          }
          break
        case 'Escape':
          event.preventDefault()
          handleClose()
          break
      }
    },
    [hasQuery, flatItems, recentSearches, selectedIndex, handleSelect, handleClose],
  )

  const handleClearRecent = useCallback(() => {
    clearRecentSearches()
    setRecentSearches([])
  }, [])

  const showRecentSearches = !hasQuery && recentSearches.length > 0

  return (
    <>
      {/* Trigger button in top bar */}
      <SearchTriggerButton onClick={handleOpen} />

      {/* Spotlight modal */}
      <Dialog
        open={open}
        onClose={handleClose}
        maxWidth={false}
        fullScreen={isMobile}
        aria-labelledby="global-search-title"
        PaperProps={{
          sx: {
            width: { xs: '100%', sm: 560 },
            maxHeight: { xs: '100vh', sm: 480 },
            borderRadius: { xs: 0, sm: '12px' },
            bgcolor: 'background.paper',
            backgroundImage: 'none',
            position: { sm: 'fixed' },
            top: { sm: '20%' },
            m: 0,
          },
        }}
        slotProps={{
          backdrop: {
            sx: { bgcolor: 'rgba(0,0,0,0.4)', backdropFilter: 'blur(4px)' },
          },
        }}
        sx={{ '& .MuiDialog-container': { alignItems: 'flex-start' } }}
      >
        {/* Search input */}
        <Box sx={{ px: 2, pt: 2, pb: 1, display: 'flex', alignItems: 'center', gap: 1 }}>
          <TextField
            inputRef={inputRef}
            id="global-search-title"
            autoFocus
            fullWidth
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Search workspaces, boards, tasks..."
            InputProps={{
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon sx={{ color: 'text.secondary' }} />
                </InputAdornment>
              ),
              endAdornment: hasQuery ? (
                <InputAdornment position="end">
                  <IconButton
                    size="small"
                    aria-label="Clear search"
                    onClick={() => setQuery('')}
                  >
                    <CloseIcon fontSize="small" />
                  </IconButton>
                </InputAdornment>
              ) : !isMobile ? (
                <InputAdornment position="end">
                  <Typography
                    variant="caption"
                    sx={{
                      px: 0.75,
                      py: 0.25,
                      borderRadius: '4px',
                      bgcolor: 'action.hover',
                      color: 'text.secondary',
                      fontSize: '0.7rem',
                      fontFamily: 'monospace',
                    }}
                  >
                    Esc
                  </Typography>
                </InputAdornment>
              ) : null,
              sx: {
                height: 48,
                fontSize: '0.9375rem',
              },
            }}
          />
          {isMobile ? (
            <IconButton
              aria-label="Close search"
              onClick={handleClose}
              sx={{ flexShrink: 0 }}
            >
              <CloseIcon />
            </IconButton>
          ) : null}
        </Box>

        {/* Results area */}
        <Box
          sx={{
            overflowY: 'auto',
            maxHeight: 'calc(480px - 80px)',
            borderTop: 1,
            borderColor: 'divider',
          }}
        >
          {/* Loading skeletons */}
          {isLoading ? (
            <Box sx={{ px: 2, py: 1.5 }}>
              {Array.from({ length: 4 }).map((_, i) => (
                <Box key={i} sx={{ display: 'flex', alignItems: 'center', gap: 1.5, py: 0.75 }}>
                  <Skeleton variant="circular" width={24} height={24} />
                  <Box sx={{ flex: 1 }}>
                    <Skeleton variant="text" width={`${60 + i * 10}%`} height={20} />
                    <Skeleton variant="text" width={`${40 + i * 5}%`} height={16} />
                  </Box>
                </Box>
              ))}
            </Box>
          ) : null}

          {/* Results */}
          {!isLoading && items.length > 0 ? (
            <List disablePadding>
              {TYPE_ORDER.map((type) => {
                const group = groupedItems[type]
                if (!group || group.length === 0) return null
                return (
                  <li key={type}>
                    <ul style={{ padding: 0 }}>
                      <ListSubheader
                        sx={{
                          lineHeight: '28px',
                          bgcolor: 'background.paper',
                          fontSize: '0.7rem',
                          fontWeight: 600,
                          textTransform: 'uppercase',
                          letterSpacing: '0.05em',
                          color: 'text.secondary',
                          px: 2,
                        }}
                      >
                        {TYPE_LABELS[type]}
                      </ListSubheader>
                      {group.map((item) => {
                        const flatIndex = flatItems.indexOf(item)
                        const isSelected = flatIndex === selectedIndex
                        return (
                          <ListItemButton
                            key={item.id}
                            selected={isSelected}
                            onClick={() => void handleSelect(item)}
                            sx={{
                              py: 0.75,
                              px: 2,
                              '&.Mui-selected': {
                                bgcolor: 'action.hover',
                              },
                              '&.Mui-selected:hover': {
                                bgcolor: 'action.hover',
                              },
                            }}
                          >
                            <ListItemIcon sx={{ minWidth: 36, color: 'text.secondary' }}>
                              {TYPE_ICONS[item.type]}
                            </ListItemIcon>
                            <ListItemText
                              primary={item.name}
                              secondary={buildBreadcrumb(item)}
                              primaryTypographyProps={{
                                variant: 'body2',
                                noWrap: true,
                                fontWeight: 500,
                              }}
                              secondaryTypographyProps={{
                                variant: 'caption',
                                noWrap: true,
                                color: 'text.secondary',
                              }}
                            />
                          </ListItemButton>
                        )
                      })}
                    </ul>
                  </li>
                )
              })}
            </List>
          ) : null}

          {/* No results */}
          {noResults ? (
            <Box sx={{ px: 3, py: 4, textAlign: 'center' }}>
              <SearchIcon sx={{ fontSize: 40, color: 'text.secondary', mb: 1, opacity: 0.5 }} />
              <Typography variant="body2" color="text.secondary">
                No results for &ldquo;{query.trim()}&rdquo;
              </Typography>
              <Typography
                variant="caption"
                color="text.secondary"
                sx={{ mt: 0.5, display: 'block' }}
              >
                Try searching for a workspace, board, or task name
              </Typography>
            </Box>
          ) : null}

          {/* Recent searches */}
          {showRecentSearches ? (
            <List disablePadding>
              <ListSubheader
                sx={{
                  lineHeight: '28px',
                  bgcolor: 'background.paper',
                  fontSize: '0.7rem',
                  fontWeight: 600,
                  textTransform: 'uppercase',
                  letterSpacing: '0.05em',
                  color: 'text.secondary',
                  px: 2,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                }}
              >
                Recent searches
                <Typography
                  component="button"
                  variant="caption"
                  onClick={handleClearRecent}
                  sx={{
                    color: 'text.secondary',
                    cursor: 'pointer',
                    border: 'none',
                    bgcolor: 'transparent',
                    textTransform: 'none',
                    letterSpacing: 'normal',
                    '&:hover': { color: 'primary.main' },
                  }}
                >
                  Clear
                </Typography>
              </ListSubheader>
              {recentSearches.map((search, index) => (
                <ListItemButton
                  key={search}
                  selected={index === selectedIndex}
                  onClick={() => setQuery(search)}
                  sx={{
                    py: 0.75,
                    px: 2,
                    '&.Mui-selected': { bgcolor: 'action.hover' },
                    '&.Mui-selected:hover': { bgcolor: 'action.hover' },
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 36, color: 'text.secondary' }}>
                    <HistoryIcon fontSize="small" />
                  </ListItemIcon>
                  <ListItemText
                    primary={search}
                    primaryTypographyProps={{ variant: 'body2', noWrap: true }}
                  />
                </ListItemButton>
              ))}
            </List>
          ) : null}

          {/* Empty state - no query, no recent */}
          {!hasQuery && recentSearches.length === 0 ? (
            <Box sx={{ px: 3, py: 4, textAlign: 'center' }}>
              <SearchIcon sx={{ fontSize: 40, color: 'text.secondary', mb: 1, opacity: 0.5 }} />
              <Typography variant="body2" color="text.secondary">
                Search across all your workspaces, boards, and tasks
              </Typography>
            </Box>
          ) : null}
        </Box>
      </Dialog>
    </>
  )
}

function SearchTriggerButton({ onClick }: { onClick: () => void }) {
  return (
    <Box
      component="button"
      onClick={onClick}
      sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 1,
        px: 1.5,
        height: 34,
        borderRadius: '8px',
        border: 1,
        borderColor: 'divider',
        bgcolor: 'background.paper',
        cursor: 'pointer',
        color: 'text.secondary',
        width: { xs: 'auto', sm: '100%' },
        maxWidth: 280,
        '&:hover': {
          borderColor: 'primary.main',
          bgcolor: 'action.hover',
        },
      }}
    >
      <SearchIcon sx={{ fontSize: 18, color: 'text.secondary' }} />
      <Typography
        variant="body2"
        sx={{
          flex: 1,
          textAlign: 'left',
          color: 'text.secondary',
          fontSize: '0.8125rem',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
          display: { xs: 'none', sm: 'block' },
        }}
      >
        Search...
      </Typography>
    </Box>
  )
}

function buildBreadcrumb(item: GlobalSearchItem): string | undefined {
  const loc = item.location
  if (!loc) return undefined

  const parts: string[] = []
  if (loc.projectName) parts.push(loc.projectName)
  if (loc.boardName) parts.push(loc.boardName)
  if (loc.columnName) parts.push(loc.columnName)

  if (parts.length === 0) return undefined
  return parts.join(' › ')
}
