import Box from '@mui/material/Box'
import Skeleton from '@mui/material/Skeleton'
import Stack from '@mui/material/Stack'

function SidebarRowSkeleton({ width }: { width: string }) {
  return (
    <Stack
      direction="row"
      alignItems="center"
      spacing={1}
      sx={{ px: 1.5, py: 1, borderBottom: '1px solid', borderColor: 'divider' }}
    >
      <Skeleton variant="circular" width={20} height={20} />
      <Skeleton variant="text" width={width} height={20} />
      <Box sx={{ flex: 1 }} />
      <Skeleton variant="rounded" width={24} height={18} sx={{ borderRadius: 3 }} />
    </Stack>
  )
}

function TimelineRowSkeleton({ barWidth, barOffset }: { barWidth: string; barOffset: string }) {
  return (
    <Box
      sx={{
        position: 'relative',
        height: 36,
        borderBottom: '1px solid',
        borderColor: 'divider',
      }}
    >
      <Skeleton
        variant="rounded"
        sx={{
          position: 'absolute',
          left: barOffset,
          top: 6,
          width: barWidth,
          height: 24,
          borderRadius: 1.5,
        }}
      />
    </Box>
  )
}

export function GanttSkeleton() {
  return (
    <Box
      aria-label="Loading timeline"
      sx={{
        display: 'flex',
        flexDirection: 'column',
        height: { xs: 'calc(100vh - 160px)', sm: 'calc(100vh - 120px)' },
        border: 1,
        borderColor: 'divider',
        borderRadius: 1,
        overflow: 'hidden',
      }}
    >
      {/* Toolbar */}
      <Stack
        direction="row"
        alignItems="center"
        spacing={1.5}
        sx={{ px: 2, py: 1.5, borderBottom: '1px solid', borderColor: 'divider' }}
      >
        <Skeleton variant="rounded" width={90} height={32} />
        <Skeleton variant="circular" width={28} height={28} />
        <Skeleton variant="text" width={120} height={24} />
        <Skeleton variant="circular" width={28} height={28} />
        <Box sx={{ flex: 1 }} />
        <Skeleton variant="rounded" width={80} height={32} />
      </Stack>

      {/* Content area: sidebar + timeline */}
      <Box sx={{ flex: 1, display: 'flex', overflow: 'hidden' }}>
        {/* Sidebar */}
        <Box sx={{ width: 240, borderRight: '1px solid', borderColor: 'divider', flexShrink: 0, display: { xs: 'none', sm: 'block' } }}>
          {/* Timeline header columns placeholder */}
          <Box sx={{ height: 36, borderBottom: '1px solid', borderColor: 'divider' }} />
          <SidebarRowSkeleton width="65%" />
          <Box sx={{ pl: 3 }}>
            <SidebarRowSkeleton width="55%" />
            <SidebarRowSkeleton width="70%" />
            <SidebarRowSkeleton width="50%" />
          </Box>
          <SidebarRowSkeleton width="60%" />
          <Box sx={{ pl: 3 }}>
            <SidebarRowSkeleton width="45%" />
            <SidebarRowSkeleton width="65%" />
          </Box>
          <SidebarRowSkeleton width="55%" />
          <Box sx={{ pl: 3 }}>
            <SidebarRowSkeleton width="60%" />
            <SidebarRowSkeleton width="50%" />
            <SidebarRowSkeleton width="70%" />
          </Box>
        </Box>

        {/* Timeline grid */}
        <Box sx={{ flex: 1, overflow: 'hidden' }}>
          {/* Date headers */}
          <Stack
            direction="row"
            sx={{ height: 36, borderBottom: '1px solid', borderColor: 'divider', px: 1 }}
          >
            {Array.from({ length: 7 }).map((_, i) => (
              <Box key={i} sx={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <Skeleton variant="text" width={40} height={16} />
              </Box>
            ))}
          </Stack>
          {/* Board header row */}
          <TimelineRowSkeleton barWidth="0%" barOffset="0%" />
          {/* Card bars */}
          <TimelineRowSkeleton barWidth="35%" barOffset="10%" />
          <TimelineRowSkeleton barWidth="20%" barOffset="25%" />
          <TimelineRowSkeleton barWidth="45%" barOffset="5%" />
          {/* Board header row */}
          <TimelineRowSkeleton barWidth="0%" barOffset="0%" />
          <TimelineRowSkeleton barWidth="30%" barOffset="40%" />
          <TimelineRowSkeleton barWidth="25%" barOffset="15%" />
          {/* Board header row */}
          <TimelineRowSkeleton barWidth="0%" barOffset="0%" />
          <TimelineRowSkeleton barWidth="50%" barOffset="20%" />
          <TimelineRowSkeleton barWidth="15%" barOffset="55%" />
          <TimelineRowSkeleton barWidth="35%" barOffset="30%" />
        </Box>
      </Box>
    </Box>
  )
}
