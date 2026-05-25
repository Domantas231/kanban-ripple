import type { ReactNode } from 'react'
import Alert from '@mui/material/Alert'
import Typography from '@mui/material/Typography'

interface ArchiveListStateProps {
  isLoading: boolean
  isError: boolean
  loadingText: string
  errorText: string
  emptyText: string
  hasItems: boolean
  children: ReactNode
}

export function ArchiveListState({
  isLoading,
  isError,
  loadingText,
  errorText,
  emptyText,
  hasItems,
  children,
}: ArchiveListStateProps) {
  if (isLoading) {
    return <Typography color="text.secondary">{loadingText}</Typography>
  }

  if (isError) {
    return <Alert severity="error">{errorText}</Alert>
  }

  if (!hasItems) {
    return <Typography color="text.secondary">{emptyText}</Typography>
  }

  return <>{children}</>
}
