import Avatar from '@mui/material/Avatar'
import Box from '@mui/material/Box'
import Card from '@mui/material/Card'
import CardActionArea from '@mui/material/CardActionArea'
import CardContent from '@mui/material/CardContent'
import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import ArchiveOutlinedIcon from '@mui/icons-material/ArchiveOutlined'
import AssignmentOutlinedIcon from '@mui/icons-material/AssignmentOutlined'
import StarIcon from '@mui/icons-material/Star'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import ViewColumnOutlinedIcon from '@mui/icons-material/ViewColumnOutlined'
import ViewKanbanOutlinedIcon from '@mui/icons-material/ViewKanbanOutlined'
import { timeAgo } from '@/utils/format'
import type { Board } from '@/lib/types'

interface BoardCardProps {
  board: Board
  cardCount: number
  columnCount: number
  canManage: boolean
  isFavorite?: boolean
  onToggleFavorite?: () => void
  onClick: () => void
  onArchive: () => void
}

export function BoardCard({
  board,
  cardCount,
  columnCount,
  canManage,
  isFavorite = false,
  onToggleFavorite,
  onClick,
  onArchive,
}: BoardCardProps) {
  return (
    <Card
      variant="outlined"
      sx={{
        height: '100%',
        position: 'relative',
        overflow: 'hidden',
        transition: 'box-shadow 150ms ease, border-color 150ms ease',
        '&:hover': {
          boxShadow: 2,
          borderColor: 'primary.main',
        },
      }}
    >
      <CardActionArea onClick={onClick} sx={{ height: '100%' }}>
        <CardContent
          sx={{
            p: { xs: 1.75, sm: 2.5 },
            height: '100%',
            '&:last-child': { pb: { xs: 1.75, sm: 2.5 } },
          }}
        >
          <Stack spacing={{ xs: 1, sm: 2 }} sx={{ height: '100%' }}>
            <Stack direction="row" alignItems="center" spacing={1.5}>
              <Avatar
                sx={{
                  width: { xs: 32, sm: 40 },
                  height: { xs: 32, sm: 40 },
                  bgcolor: (theme) =>
                    theme.palette.mode === 'dark'
                      ? 'rgba(20,184,166,0.12)'
                      : 'rgba(13,148,136,0.08)',
                  color: 'primary.main',
                }}
              >
                <ViewKanbanOutlinedIcon sx={{ fontSize: { xs: 18, sm: 20 } }} />
              </Avatar>
              <Typography variant="subtitle1" sx={{ fontWeight: 600, flex: 1, minWidth: 0 }} noWrap>
                {board.name}
              </Typography>
              <Stack direction="row" spacing={0} sx={{ flexShrink: 0 }}>
                {onToggleFavorite ? (
                  <IconButton
                    size="small"
                    aria-label={isFavorite ? 'Remove from favorites' : 'Add to favorites'}
                    onClick={(e) => {
                      e.stopPropagation()
                      e.preventDefault()
                      onToggleFavorite()
                    }}
                    onPointerDown={(e) => e.stopPropagation()}
                  >
                    {isFavorite ? (
                      <StarIcon sx={{ fontSize: 18, color: 'warning.main' }} />
                    ) : (
                      <StarBorderIcon sx={{ fontSize: 18, color: 'text.disabled' }} />
                    )}
                  </IconButton>
                ) : null}
                {canManage ? (
                  <IconButton
                    size="small"
                    onClick={(e) => {
                      e.stopPropagation()
                      e.preventDefault()
                      onArchive()
                    }}
                    onPointerDown={(e) => e.stopPropagation()}
                    aria-label={`Archive board ${board.name}`}
                    sx={{ display: { xs: 'inline-flex', sm: 'none' } }}
                  >
                    <ArchiveOutlinedIcon sx={{ fontSize: 18 }} />
                  </IconButton>
                ) : null}
              </Stack>
            </Stack>

            {/* Mobile: combined stats + updated time on one row */}
            <Stack
              direction="row"
              spacing={1.5}
              alignItems="center"
              sx={{
                color: 'text.secondary',
                display: { xs: 'flex', sm: 'none' },
                flexWrap: 'wrap',
              }}
            >
              <Stack direction="row" alignItems="center" spacing={0.5}>
                <ViewColumnOutlinedIcon sx={{ fontSize: 14 }} />
                <Typography variant="caption">
                  {columnCount} {columnCount === 1 ? 'list' : 'lists'}
                </Typography>
              </Stack>
              <Box component="span" sx={{ color: 'text.disabled' }}>·</Box>
              <Stack direction="row" alignItems="center" spacing={0.5}>
                <AssignmentOutlinedIcon sx={{ fontSize: 14 }} />
                <Typography variant="caption">
                  {cardCount} {cardCount === 1 ? 'task' : 'tasks'}
                </Typography>
              </Stack>
              <Box component="span" sx={{ color: 'text.disabled' }}>·</Box>
              <Typography variant="caption" color="text.disabled">
                {timeAgo(board.updatedAt)}
              </Typography>
            </Stack>

            {/* Tablet+ : separate stats row */}
            <Stack
              direction="row"
              spacing={2}
              alignItems="center"
              sx={{ color: 'text.secondary', display: { xs: 'none', sm: 'flex' } }}
            >
              <Stack direction="row" alignItems="center" spacing={0.5}>
                <ViewColumnOutlinedIcon sx={{ fontSize: 14 }} />
                <Typography variant="caption">
                  {columnCount} {columnCount === 1 ? 'list' : 'lists'}
                </Typography>
              </Stack>
              <Stack direction="row" alignItems="center" spacing={0.5}>
                <AssignmentOutlinedIcon sx={{ fontSize: 14 }} />
                <Typography variant="caption">
                  {cardCount} {cardCount === 1 ? 'task' : 'tasks'}
                </Typography>
              </Stack>
            </Stack>

            {/* Tablet+ : separate updated + archive row */}
            <Stack
              direction="row"
              justifyContent="space-between"
              alignItems="center"
              sx={{ mt: 'auto', display: { xs: 'none', sm: 'flex' } }}
            >
              <Typography variant="caption" color="text.disabled">
                Updated {timeAgo(board.updatedAt)}
              </Typography>
              {canManage ? (
                <IconButton
                  size="small"
                  onClick={(e) => {
                    e.stopPropagation()
                    e.preventDefault()
                    onArchive()
                  }}
                  onPointerDown={(e) => e.stopPropagation()}
                  aria-label={`Archive board ${board.name}`}
                >
                  <ArchiveOutlinedIcon sx={{ fontSize: 16 }} />
                </IconButton>
              ) : null}
            </Stack>
          </Stack>
        </CardContent>
      </CardActionArea>
    </Card>
  )
}
