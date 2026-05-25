import Tooltip from '@mui/material/Tooltip'
import SyncIcon from '@mui/icons-material/Sync'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline'
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline'
import type { PlannedBlockSyncStatus } from '@/lib/types'

type PlannerSyncIndicatorProps = {
  status: PlannedBlockSyncStatus
}

export function PlannerSyncIndicator({ status }: PlannerSyncIndicatorProps) {
  switch (status) {
    case 0:
      return (
        <Tooltip title="Pending sync">
          <SyncIcon sx={{ fontSize: 16, color: 'text.secondary' }} />
        </Tooltip>
      )
    case 1:
      return (
        <Tooltip title="Synced to Google Calendar">
          <CheckCircleOutlineIcon sx={{ fontSize: 16, color: '#fff' }} />
        </Tooltip>
      )
    case 2:
      return (
        <Tooltip title="Sync failed">
          <ErrorOutlineIcon sx={{ fontSize: 16, color: 'error.main' }} />
        </Tooltip>
      )
    default:
      return null
  }
}
