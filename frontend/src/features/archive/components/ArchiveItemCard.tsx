import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { formatDeletedAt } from '@/features/archive/utils/archiveFormatters'

interface ArchiveItemCardProps {
  name: string
  location: string
  deletedAt?: string | null
  canRestore: boolean
  onRestore: () => void
  restorePending: boolean
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
  canDelete = false,
  onDelete,
  deletePending = false,
}: ArchiveItemCardProps) {
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
              <Button
                variant="outlined"
                size="small"
                onClick={onRestore}
                disabled={restorePending || deletePending}
                sx={{ width: 'fit-content' }}
              >
                {restorePending ? 'Restoring...' : 'Restore'}
              </Button>
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
