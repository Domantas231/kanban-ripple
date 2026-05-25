import Box from '@mui/material/Box'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import Skeleton from '@mui/material/Skeleton'
import Stack from '@mui/material/Stack'

const tealBorder = (theme: { palette: { mode: string } }) =>
  theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.15)' : 'rgba(13, 148, 136, 0.12)'

export function CardDetailSkeleton() {
  return (
    <>
      {/* Header bar */}
      <Box sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 1.5,
        px: 3,
        pt: 2,
        pb: 1.5,
        borderBottom: '1px solid',
        borderColor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.12)' : 'rgba(13, 148, 136, 0.1)',
        bgcolor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.04)' : 'rgba(13, 148, 136, 0.03)',
      }}>
        <Skeleton variant="rounded" width={80} height={24} />
        <Skeleton variant="text" width={40} height={16} />
        <Box sx={{ flex: 1 }} />
        <Skeleton variant="rounded" width={90} height={28} />
        <Skeleton variant="circular" width={28} height={28} />
      </Box>

      <DialogContent sx={{ px: 3, pt: 1.5, pb: 3 }}>
        <Box sx={{ display: 'flex', gap: 4, flexDirection: { xs: 'column', md: 'row' } }}>
          {/* Left column */}
          <Box sx={{ flex: '1 1 0%', minWidth: 0 }}>
            <Stack spacing={3}>
              <Skeleton variant="text" width="55%" height={40} />
              <Stack direction="row" spacing={1}>
                <Skeleton variant="rounded" width={70} height={30} />
                <Skeleton variant="rounded" width={70} height={30} />
              </Stack>
              <Stack spacing={0.75}>
                <Skeleton variant="text" width="20%" height={20} />
                <Skeleton variant="rounded" width="100%" height={100} sx={{ borderRadius: 1.5 }} />
              </Stack>
              <Stack spacing={0.75}>
                <Skeleton variant="text" width="25%" height={24} />
                <Skeleton variant="rounded" width="100%" height={4} sx={{ borderRadius: 2 }} />
                <Skeleton variant="rounded" width="100%" height={36} />
                <Skeleton variant="rounded" width="100%" height={36} />
              </Stack>
              <Stack spacing={1}>
                <Skeleton variant="text" width="22%" height={24} />
                <Skeleton variant="rounded" width="100%" height={56} sx={{ borderRadius: '0 8px 8px 0' }} />
              </Stack>
            </Stack>
          </Box>
          {/* Right sidebar */}
          <Box sx={{ flex: '0 0 300px' }}>
            <Stack spacing={2}>
              <Box sx={{ borderRadius: 2, border: '1px solid', borderColor: tealBorder, overflow: 'hidden' }}>
                <Box sx={{ px: 2, pt: 1.5, pb: 1 }}>
                  <Skeleton variant="text" width="50%" height={14} />
                </Box>
                <Stack spacing={0}>
                  {[1, 2, 3, 4, 5].map((i) => (
                    <Box key={i} sx={{ px: 2, py: 1.25, display: 'flex', alignItems: 'center' }}>
                      <Skeleton variant="text" width="30%" height={18} />
                      <Box sx={{ flex: 1 }} />
                      <Skeleton variant="rounded" width="45%" height={18} />
                    </Box>
                  ))}
                </Stack>
              </Box>
              <Box sx={{ borderRadius: 2, border: '1px solid', borderColor: tealBorder, p: 2 }}>
                <Skeleton variant="text" width="40%" height={14} />
                <Skeleton variant="rounded" width="100%" height={48} sx={{ mt: 1 }} />
              </Box>
            </Stack>
          </Box>
        </Box>
      </DialogContent>
      <DialogActions sx={{
        px: 3,
        py: 2,
        borderTop: '1px solid',
        borderColor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.12)' : 'rgba(13, 148, 136, 0.1)',
      }}>
        <Box sx={{ flex: 1 }} />
        <Skeleton variant="rounded" width={70} height={32} />
        <Skeleton variant="rounded" width={100} height={32} />
      </DialogActions>
    </>
  )
}
