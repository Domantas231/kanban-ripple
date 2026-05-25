import Box from '@mui/material/Box'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Grid from '@mui/material/Grid'
import Skeleton from '@mui/material/Skeleton'
import Stack from '@mui/material/Stack'

export function ProjectGallerySkeleton() {
  return (
    <Grid container spacing={2.5}>
      {Array.from({ length: 6 }).map((_, i) => (
        <Grid key={i} size={{ xs: 12, sm: 6, lg: 4 }}>
          <Card variant="outlined" sx={{ overflow: 'hidden' }}>
            <Skeleton variant="rectangular" height={6} />
            <CardContent sx={{ p: 2.5 }}>
              <Stack spacing={2}>
                <Stack direction="row" spacing={1.5} alignItems="center">
                  <Skeleton variant="circular" width={40} height={40} />
                  <Box sx={{ flex: 1 }}>
                    <Skeleton variant="text" width="60%" height={24} />
                    <Skeleton variant="rounded" width={48} height={20} sx={{ mt: 0.5 }} />
                  </Box>
                </Stack>
                <Stack direction="row" justifyContent="space-between" alignItems="center">
                  <Skeleton variant="text" width={80} height={18} />
                  <Skeleton variant="text" width={60} height={18} />
                </Stack>
              </Stack>
            </CardContent>
          </Card>
        </Grid>
      ))}
    </Grid>
  )
}
