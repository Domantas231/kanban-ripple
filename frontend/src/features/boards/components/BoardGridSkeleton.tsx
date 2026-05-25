import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Grid from '@mui/material/Grid'
import Skeleton from '@mui/material/Skeleton'
import Stack from '@mui/material/Stack'

export function BoardGridSkeleton() {
  return (
    <Grid container spacing={2.5}>
      {Array.from({ length: 3 }).map((_, i) => (
        <Grid key={i} size={{ xs: 12, sm: 6, lg: 4 }}>
          <Card variant="outlined" sx={{ overflow: 'hidden' }}>
            <CardContent sx={{ p: 2.5 }}>
              <Stack spacing={2}>
                <Stack direction="row" spacing={1.5} alignItems="center">
                  <Skeleton variant="circular" width={40} height={40} />
                  <Skeleton variant="text" width="60%" height={24} />
                </Stack>
                <Stack direction="row" spacing={2}>
                  <Skeleton variant="text" width={80} height={18} />
                  <Skeleton variant="text" width={60} height={18} />
                </Stack>
                <Skeleton variant="text" width={100} height={16} />
              </Stack>
            </CardContent>
          </Card>
        </Grid>
      ))}
    </Grid>
  )
}
