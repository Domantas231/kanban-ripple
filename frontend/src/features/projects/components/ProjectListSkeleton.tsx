import Box from '@mui/material/Box'
import Card from '@mui/material/Card'
import List from '@mui/material/List'
import ListItem from '@mui/material/ListItem'
import Skeleton from '@mui/material/Skeleton'

export function ProjectListSkeleton() {
  return (
    <Card variant="outlined">
      <List disablePadding>
        {Array.from({ length: 5 }).map((_, i) => (
          <ListItem key={i} divider={i < 4} sx={{ py: 1.5, px: 2.5 }}>
            <Skeleton variant="circular" width={36} height={36} sx={{ mr: 2 }} />
            <Box sx={{ flex: 1 }}>
              <Skeleton variant="text" width="40%" height={22} />
              <Skeleton variant="text" width="30%" height={16} />
            </Box>
          </ListItem>
        ))}
      </List>
    </Card>
  )
}
