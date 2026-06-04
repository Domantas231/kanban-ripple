import { Box, Skeleton, Stack } from '@mui/material'
import { CardListSkeleton } from './CardListSkeleton'

type BoardSkeletonProps = {
  /** Number of placeholder columns to render. */
  columnCount?: number
  /** Number of placeholder cards rendered inside each column. */
  cardsPerColumn?: number
}

/**
 * Full-board loading placeholder that mirrors the columns rail layout so the
 * board doesn't flash empty columns while cards are still loading. Shown until
 * both columns and cards have resolved, then swapped for the real board.
 */
export function BoardSkeleton({ columnCount = 4, cardsPerColumn = 3 }: BoardSkeletonProps) {
  return (
    <Box sx={{ overflowX: 'auto', pb: { xs: 0.5, sm: 1 } }} aria-label="Loading board">
      <Stack direction="row" spacing={{ xs: 1, sm: 2 }} alignItems="flex-start" sx={{ minWidth: 'min-content' }}>
        {Array.from({ length: columnCount }).map((_, index) => (
          <Box
            key={index}
            sx={{
              flex: { xs: '0 0 calc(100cqw - 24px)', sm: '0 0 300px' },
              minWidth: 0,
              borderRadius: 2,
              border: '1px solid',
              borderColor: (theme) =>
                theme.palette.mode === 'dark' ? 'rgba(255,255,255,0.06)' : 'rgba(190,210,235,0.5)',
              bgcolor: (theme) =>
                theme.palette.mode === 'dark' ? 'rgba(255,255,255,0.03)' : 'rgba(220,230,245,0.35)',
              px: { xs: 0.5, sm: 0.75 },
              py: { xs: 1, sm: 1.25 },
            }}
          >
            <Stack spacing={1.5}>
              {/* Column header */}
              <Stack
                direction="row"
                alignItems="center"
                justifyContent="space-between"
                sx={{ px: 0.25 }}
              >
                <Skeleton variant="text" width="55%" height={24} />
                <Skeleton variant="rounded" width={24} height={20} />
              </Stack>

              <CardListSkeleton count={cardsPerColumn} />
            </Stack>
          </Box>
        ))}
      </Stack>
    </Box>
  )
}
