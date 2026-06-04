import { useNavigate } from '@tanstack/react-router'
import Avatar from '@mui/material/Avatar'
import Card from '@mui/material/Card'
import IconButton from '@mui/material/IconButton'
import List from '@mui/material/List'
import ListItem from '@mui/material/ListItem'
import ListItemButton from '@mui/material/ListItemButton'
import ListItemText from '@mui/material/ListItemText'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import ArchiveOutlinedIcon from '@mui/icons-material/ArchiveOutlined'
import StarIcon from '@mui/icons-material/Star'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import ViewKanbanOutlinedIcon from '@mui/icons-material/ViewKanbanOutlined'
import { timeAgo } from '@/utils/format'
import type { Board } from '@/lib/types'

interface BoardListViewProps {
  boards: Board[]
  canManage: boolean
  projectId: string
  onArchive: (board: Board) => void
  favoriteBoardIds?: Set<string>
  onToggleFavorite?: (boardId: string) => void
}

export function BoardListView({
  boards,
  canManage,
  projectId,
  onArchive,
  favoriteBoardIds,
  onToggleFavorite,
}: BoardListViewProps) {
  const navigate = useNavigate()

  return (
    <Card variant="outlined">
        <List disablePadding>
          {boards.map((board, index) => {
            const cardCount = board.cardCount ?? 0
            const columnCount = board.columnCount ?? 0

            return (
              <ListItem
                key={board.id}
                disablePadding
                divider={index < boards.length - 1}
                secondaryAction={
                  <Stack direction="row" spacing={0} alignItems="center">
                    {favoriteBoardIds && onToggleFavorite ? (
                      <IconButton
                        size="small"
                        aria-label={
                          favoriteBoardIds.has(board.id)
                            ? 'Remove from favorites'
                            : 'Add to favorites'
                        }
                        onClick={() => onToggleFavorite(board.id)}
                      >
                        {favoriteBoardIds.has(board.id) ? (
                          <StarIcon fontSize="small" sx={{ color: 'warning.main' }} />
                        ) : (
                          <StarBorderIcon fontSize="small" sx={{ color: 'text.disabled' }} />
                        )}
                      </IconButton>
                    ) : null}
                    {canManage ? (
                      <IconButton
                        edge="end"
                        aria-label={`Archive ${board.name}`}
                        onClick={() => onArchive(board)}
                      >
                        <ArchiveOutlinedIcon fontSize="small" />
                      </IconButton>
                    ) : null}
                  </Stack>
                }
              >
                <ListItemButton
                  onClick={() =>
                    navigate({
                      to: '/projects/$projectId/boards/$boardId',
                      params: { projectId, boardId: board.id },
                    })
                  }
                  sx={{ py: 1.5, px: 2.5 }}
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
                      <Stack direction="row" spacing={2} component="span" sx={{ mt: 0.25 }}>
                        <Typography variant="caption" color="text.secondary" component="span">
                          {columnCount} {columnCount === 1 ? 'list' : 'lists'}
                        </Typography>
                        <Typography variant="caption" color="text.secondary" component="span">
                          {cardCount} {cardCount === 1 ? 'task' : 'tasks'}
                        </Typography>
                        <Typography variant="caption" color="text.disabled" component="span">
                          {timeAgo(board.updatedAt)}
                        </Typography>
                      </Stack>
                    }
                  />
                </ListItemButton>
              </ListItem>
            )
          })}
        </List>
      </Card>
  )
}
