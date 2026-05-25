import Box from '@mui/material/Box'
import Skeleton from '@mui/material/Skeleton'
import Stack from '@mui/material/Stack'

export function PlannerSkeleton() {
  return (
    <Stack spacing={3}>
      {/* Day navigation skeleton */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <Skeleton variant="circular" width={32} height={32} />
        <Skeleton variant="circular" width={32} height={32} />
        <Skeleton variant="text" width={100} height={36} />
        <Skeleton variant="text" width={120} height={28} />
        <Box sx={{ ml: 'auto' }}>
          <Skeleton variant="rounded" width={160} height={36} sx={{ borderRadius: 1 }} />
        </Box>
      </Box>

      {/* Main content skeleton */}
      <Box sx={{ display: 'flex', gap: 0, height: 600, border: 1, borderColor: 'divider', borderRadius: 2, overflow: 'hidden' }}>
        {/* Sidebar skeleton */}
        <Box sx={{ width: 280, minWidth: 280, borderRight: 1, borderColor: 'divider', p: 2 }}>
          <Skeleton variant="text" width={120} height={20} sx={{ mb: 2 }} />
          <Stack spacing={1}>
            {[1, 2, 3, 4, 5].map((i) => (
              <Skeleton key={i} variant="rounded" height={56} sx={{ borderRadius: 1 }} />
            ))}
          </Stack>
        </Box>

        {/* Timeline skeleton */}
        <Box sx={{ flex: 1, p: 2 }}>
          <Stack spacing={0}>
            {[1, 2, 3, 4, 5, 6, 7, 8].map((i) => (
              <Box key={i} sx={{ display: 'flex', alignItems: 'center', gap: 1, height: 60 }}>
                <Skeleton variant="text" width={44} height={16} />
                <Box sx={{ flex: 1, height: '1px', bgcolor: 'divider' }} />
              </Box>
            ))}
          </Stack>
        </Box>
      </Box>
    </Stack>
  )
}
