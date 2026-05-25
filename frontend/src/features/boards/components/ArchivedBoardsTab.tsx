import { useMemo } from 'react'
import Avatar from '@mui/material/Avatar'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Grid from '@mui/material/Grid'
import List from '@mui/material/List'
import ListItem from '@mui/material/ListItem'
import ListItemText from '@mui/material/ListItemText'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import ArchiveOutlinedIcon from '@mui/icons-material/ArchiveOutlined'
import SearchIcon from '@mui/icons-material/Search'
import ViewKanbanOutlinedIcon from '@mui/icons-material/ViewKanbanOutlined'
import { EmptyState } from '@/components/feedback/EmptyState'
import {
  useArchivedBoards,
  usePurgeBoard,
  useRestoreBoard,
} from '@/features/boards/api/boards'
import { BoardGridSkeleton } from '@/features/boards/components/BoardGridSkeleton'
import { timeAgo } from '@/utils/format'
import type { Board } from '@/lib/types'

interface ArchivedBoardsTabProps {
  projectId: string
  viewMode: 'grid' | 'list'
  search: string
  canManage?: boolean
}

export function ArchivedBoardsTab({ projectId, viewMode, search, canManage = false }: ArchivedBoardsTabProps) {
  const archivedBoardsQuery = useArchivedBoards()
  const restoreBoardMutation = useRestoreBoard()
  const purgeBoardMutation = usePurgeBoard()

  const allProjectArchivedBoards = useMemo(
    () => (archivedBoardsQuery.data ?? []).filter((b: Board) => b.projectId === projectId),
    [archivedBoardsQuery.data, projectId],
  )

  const projectArchivedBoards = useMemo(() => {
    const trimmed = search.trim().toLowerCase()
    if (!trimmed) return allProjectArchivedBoards
    return allProjectArchivedBoards.filter((b) => b.name.toLowerCase().includes(trimmed))
  }, [allProjectArchivedBoards, search])

  if (archivedBoardsQuery.isLoading) {
    return <BoardGridSkeleton />
  }

  if (allProjectArchivedBoards.length === 0) {
    return (
      <Stack alignItems="center" spacing={1.5} sx={{ py: 8 }}>
        <ArchiveOutlinedIcon sx={{ fontSize: 48, color: 'text.disabled' }} />
        <Typography variant="body1" color="text.secondary">
          No archived boards
        </Typography>
      </Stack>
    )
  }

  if (projectArchivedBoards.length === 0) {
    return (
      <EmptyState icon={SearchIcon} title={`No archived boards matching “${search.trim()}”`} compact />
    )
  }

  if (viewMode === 'list') {
    return (
      <Card variant="outlined" sx={{ opacity: 0.7 }}>
        <List disablePadding>
          {projectArchivedBoards.map((board: Board, index: number) => (
            <ListItem
              key={board.id}
              divider={index < projectArchivedBoards.length - 1}
              sx={{ py: 1.5, px: 2.5 }}
              secondaryAction={
                <Stack direction="row" spacing={1}>
                  <Button
                    size="small"
                    variant="outlined"
                    onClick={() => restoreBoardMutation.mutate(board.id)}
                    disabled={restoreBoardMutation.isPending || purgeBoardMutation.isPending}
                  >
                    Restore
                  </Button>
                  {canManage ? (
                    <Button
                      size="small"
                      variant="outlined"
                      color="error"
                      onClick={() => purgeBoardMutation.mutate(board.id)}
                      disabled={restoreBoardMutation.isPending || purgeBoardMutation.isPending}
                    >
                      Delete permanently
                    </Button>
                  ) : null}
                </Stack>
              }
            >
              <Avatar
                sx={{
                  width: 36,
                  height: 36,
                  bgcolor: (theme) =>
                    theme.palette.mode === 'dark'
                      ? 'rgba(20,184,166,0.12)'
                      : 'rgba(13,148,136,0.08)',
                  color: 'primary.main',
                  mr: 2,
                  flexShrink: 0,
                }}
              >
                <ViewKanbanOutlinedIcon sx={{ fontSize: 18 }} />
              </Avatar>
              <ListItemText
                primary={
                  <Typography variant="subtitle2" noWrap sx={{ fontWeight: 600 }}>
                    {board.name}
                  </Typography>
                }
                secondary={
                  <Typography variant="caption" color="text.disabled" component="span">
                    Archived {timeAgo(board.updatedAt)}
                  </Typography>
                }
              />
            </ListItem>
          ))}
        </List>
      </Card>
    )
  }

  return (
    <Grid container spacing={2.5}>
      {projectArchivedBoards.map((board: Board) => (
        <Grid key={board.id} size={{ xs: 12, sm: 6, lg: 4 }}>
          <Card variant="outlined" sx={{ opacity: 0.7 }}>
            <CardContent sx={{ p: 2.5 }}>
              <Stack spacing={2}>
                <Stack direction="row" alignItems="center" spacing={1.5}>
                  <Avatar
                    sx={{
                      width: 40,
                      height: 40,
                      bgcolor: (theme) =>
                        theme.palette.mode === 'dark'
                          ? 'rgba(20,184,166,0.12)'
                          : 'rgba(13,148,136,0.08)',
                      color: 'primary.main',
                    }}
                  >
                    <ViewKanbanOutlinedIcon sx={{ fontSize: 20 }} />
                  </Avatar>
                  <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Typography variant="subtitle1" sx={{ fontWeight: 600 }} noWrap>
                      {board.name}
                    </Typography>
                    <Typography variant="caption" color="text.disabled">
                      Archived {timeAgo(board.updatedAt)}
                    </Typography>
                  </Box>
                </Stack>
                <Stack direction="row" spacing={1} justifyContent="flex-end">
                  <Button
                    size="small"
                    variant="outlined"
                    onClick={() => restoreBoardMutation.mutate(board.id)}
                    disabled={restoreBoardMutation.isPending || purgeBoardMutation.isPending}
                  >
                    Restore
                  </Button>
                  {canManage ? (
                    <Button
                      size="small"
                      variant="outlined"
                      color="error"
                      onClick={() => purgeBoardMutation.mutate(board.id)}
                      disabled={restoreBoardMutation.isPending || purgeBoardMutation.isPending}
                    >
                      Delete permanently
                    </Button>
                  ) : null}
                </Stack>
              </Stack>
            </CardContent>
          </Card>
        </Grid>
      ))}
    </Grid>
  )
}
