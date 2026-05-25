import type { ReactNode } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import type { SvgIconComponent } from '@mui/icons-material'

type EmptyStateProps = {
  icon: SvgIconComponent
  title: string
  description?: string
  actionLabel?: string
  onAction?: () => void
  actionIcon?: ReactNode
  compact?: boolean
}

export function EmptyState({
  icon: Icon,
  title,
  description,
  actionLabel,
  onAction,
  actionIcon,
  compact = false,
}: EmptyStateProps) {
  return (
    <Stack
      alignItems="center"
      spacing={compact ? 1 : 2}
      sx={{ py: compact ? 4 : 10, px: 3 }}
    >
      <Box
        sx={{
          width: compact ? 56 : 80,
          height: compact ? 56 : 80,
          borderRadius: '50%',
          bgcolor: 'action.hover',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Icon sx={{ fontSize: compact ? 28 : 40, color: 'text.disabled' }} />
      </Box>
      <Typography
        variant={compact ? 'body1' : 'h3'}
        color="text.secondary"
        fontWeight={compact ? 500 : undefined}
      >
        {title}
      </Typography>
      {description ? (
        <Typography
          variant="body2"
          color="text.secondary"
          sx={{ maxWidth: 400, textAlign: 'center' }}
        >
          {description}
        </Typography>
      ) : null}
      {actionLabel && onAction ? (
        <Button
          variant="contained"
          startIcon={actionIcon}
          onClick={onAction}
          sx={{ mt: 1 }}
        >
          {actionLabel}
        </Button>
      ) : null}
    </Stack>
  )
}
