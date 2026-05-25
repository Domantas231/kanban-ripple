import { Card, CardContent, Skeleton, Stack } from '@mui/material'

type CardListSkeletonProps = {
  count?: number
}

export function CardListSkeleton({ count = 4 }: CardListSkeletonProps) {
  return (
    <Stack spacing={1} aria-label="Loading tasks">
      {Array.from({ length: count }).map((_, index) => (
        <Card key={index} variant="outlined">
          <CardContent sx={{ p: 1.25, '&:last-child': { pb: 1.25 } }}>
            <Stack spacing={1.25}>
              <Skeleton variant="text" width="75%" height={24} />
              <Stack direction="row" spacing={0.75}>
                <Skeleton variant="rounded" width={58} height={22} />
                <Skeleton variant="rounded" width={72} height={22} />
              </Stack>
              <Stack direction="row" justifyContent="space-between" alignItems="center">
                <Stack direction="row" spacing={0.5}>
                  <Skeleton variant="circular" width={24} height={24} />
                  <Skeleton variant="circular" width={24} height={24} />
                </Stack>
                <Skeleton variant="text" width={36} height={18} />
              </Stack>
            </Stack>
          </CardContent>
        </Card>
      ))}
    </Stack>
  )
}
