import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Stack from '@mui/material/Stack'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { formatDeletedAt } from '@/features/archive/utils/archiveFormatters'

interface ArchiveItemCardProps {
  name: string
  location: string
  deletedAt?: string | null
  canRestore: boolean
  onRestore: () => void
  restorePending: boolean
  restoreDisabledReason?: string | null
  canDelete?: boolean
  onDelete?: () => void
  deletePending?: boolean
}

export function ArchiveItemCard({
  name,
  location,
  deletedAt,
  canRestore,
  onRestore,
  restorePending,
  restoreDisabledReason = null,
  canDelete = false,
  onDelete,
  deletePending = false,
}: ArchiveItemCardProps) {
  const isRestoreBlocked = Boolean(restoreDisabledReason)
  const restoreButton = (
    <Button
      variant="outlined"
      size="small"
      onClick={onRestore}
      disabled={restorePending || deletePending || isRestoreBlocked}
      sx={{ width: 'fit-content' }}
    >
      {restorePending ? 'Restoring...' : 'Restore'}
    </Button>
  )
  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={1.25}>
          <Typography variant="h6">{name}</Typography>
          <Typography variant="body2" color="text.secondary">
            Original location: {location}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Archived: {formatDeletedAt(deletedAt)}
          </Typography>
          <Stack direction="row" spacing={1}>
            {canRestore ? (
              isRestoreBlocked ? (
                <Tooltip title={restoreDisabledReason}>
                  <span style={{ width: 'fit-content' }}>{restoreButton}</span>
                </Tooltip>
              ) : (
                restoreButton
              )
            ) : null}
            {canDelete && onDelete ? (
              <Button
                variant="outlined"
                color="error"
                size="small"
                onClick={onDelete}
                disabled={deletePending || restorePending}
                sx={{ width: 'fit-content' }}
              >
                {deletePending ? 'Deleting...' : 'Delete permanently'}
              </Button>
            ) : null}
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  )
}
