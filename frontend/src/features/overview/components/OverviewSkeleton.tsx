import Box from '@mui/material/Box'
import Skeleton from '@mui/material/Skeleton'
import Stack from '@mui/material/Stack'

export function OverviewSkeleton() {
  return (
    <Stack spacing={5}>
      {/* Section 1: Boards */}
      <Box>
        <Skeleton variant="text" width={60} height={20} sx={{ mb: 2 }} />
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', md: 'repeat(3, 1fr)' },
            gap: 2,
          }}
        >
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} variant="rounded" height={130} sx={{ borderRadius: 2 }} />
          ))}
        </Box>
      </Box>

      {/* Section 2: Tasks & Activity (2x2 grid) */}
      <Box>
        <Skeleton variant="text" width={120} height={20} sx={{ mb: 2 }} />
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' },
            gap: 4,
          }}
        >
          {[1, 2, 3, 4].map((i) => (
            <Box key={i}>
              <Skeleton variant="text" width={160} height={32} sx={{ mb: 2 }} />
              <Stack spacing={1}>
                {[1, 2, 3].map((j) => (
                  <Skeleton key={j} variant="rounded" height={52} sx={{ borderRadius: 2 }} />
                ))}
              </Stack>
            </Box>
          ))}
        </Box>
      </Box>

      {/* Section 3: Team & Tags */}
      <Box>
        <Skeleton variant="text" width={100} height={20} sx={{ mb: 2 }} />
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' },
            gap: 4,
          }}
        >
          <Box>
            <Skeleton variant="text" width={140} height={32} sx={{ mb: 2 }} />
            <Stack spacing={1.5}>
              {[1, 2, 3].map((i) => (
                <Skeleton key={i} variant="rounded" height={36} sx={{ borderRadius: 1 }} />
              ))}
            </Stack>
          </Box>
          <Box>
            <Skeleton variant="text" width={100} height={32} sx={{ mb: 2 }} />
            <Stack spacing={1.5}>
              {[1, 2, 3].map((i) => (
                <Skeleton key={i} variant="rounded" height={36} sx={{ borderRadius: 1 }} />
              ))}
            </Stack>
          </Box>
        </Box>
      </Box>
    </Stack>
  )
}
