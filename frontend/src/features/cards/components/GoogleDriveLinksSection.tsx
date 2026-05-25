import AddLinkIcon from '@mui/icons-material/AddLink'
import GoogleIcon from '@mui/icons-material/Google'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import CircularProgress from '@mui/material/CircularProgress'
import MenuItem from '@mui/material/MenuItem'
import Select from '@mui/material/Select'
import Stack from '@mui/material/Stack'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { getCardGoogleDriveLinks, getGoogleStatus, linkGoogleDriveFiles, unlinkGoogleDriveFile, updateDriveLinkPermission } from '@/features/planner'
import { useGooglePicker } from '@/features/planner'
import { cardsQueryKeys } from '@/features/cards/api/query-keys'
import { plannerQueryKeys } from '@/features/planner'
import { useUiStore } from '@/stores/uiStore'
import type { DriveSharePermission, Guid, PermissionReport, PermissionRevokeReport } from '@/lib/types'
import { GoogleDriveLinkItem } from './GoogleDriveLinkItem'

type GoogleDriveLinksSectionProps = {
  cardId: Guid
  canManageCards: boolean
  currentUserId?: Guid
  isCardCreator: boolean
  isProjectManager: boolean
}

function formatPermissionReport(report: PermissionReport): string {
  const parts: string[] = []
  if (report.sharedCount > 0) {
    parts.push(`Shared with ${report.sharedCount} teammate(s)`)
  }
  if (report.alreadySharedCount > 0) {
    parts.push(`${report.alreadySharedCount} already had access`)
  }
  if (report.failedCount > 0) {
    parts.push(`Failed to share with: ${report.failedEmails.join(', ')}`)
  }
  return parts.join('. ')
}

function formatRevokeReport(report: PermissionRevokeReport): string {
  const parts: string[] = []
  if (report.revokedCount > 0) {
    parts.push(`Revoked access from ${report.revokedCount} teammate(s)`)
  }
  if (report.failedCount > 0) {
    parts.push(`Failed to revoke for: ${report.failedEmails.join(', ')}`)
  }
  return parts.join('. ')
}

function GoogleDriveLinksSection({ cardId, canManageCards, currentUserId, isCardCreator, isProjectManager }: GoogleDriveLinksSectionProps) {
  const queryClient = useQueryClient()
  const enqueueToast = useUiStore(state => state.enqueueToast)
  const { openPicker, isLoading: isPickerLoading } = useGooglePicker()
  const [sharePermission, setSharePermission] = useState<DriveSharePermission>('reader')

  const linksQuery = useQuery({
    queryKey: cardsQueryKeys.cardGoogleDriveLinks(cardId),
    queryFn: () => getCardGoogleDriveLinks(cardId),
  })

  const googleStatusQuery = useQuery({
    queryKey: plannerQueryKeys.googleStatus,
    queryFn: getGoogleStatus,
  })

  const linkFilesMutation = useMutation({
    mutationFn: (googleFileIds: string[]) => linkGoogleDriveFiles(cardId, googleFileIds, sharePermission),
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardGoogleDriveLinks(cardId) })
      const reportMsg = formatPermissionReport(result.permissionReport)
      if (reportMsg) {
        enqueueToast({
          message: reportMsg,
          severity: result.permissionReport.failedCount > 0 ? 'warning' : 'success',
          durationMs: 6000,
        })
      } else {
        enqueueToast({ message: 'File(s) linked successfully', severity: 'success' })
      }
    },
    onError: () => {
      enqueueToast({ message: 'Failed to link files', severity: 'error' })
    },
  })

  const updatePermissionMutation = useMutation({
    mutationFn: ({ linkId, permission }: { linkId: string; permission: DriveSharePermission }) =>
      updateDriveLinkPermission(linkId, permission),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardGoogleDriveLinks(cardId) })
      enqueueToast({ message: 'Permission updated', severity: 'success' })
    },
    onError: () => {
      enqueueToast({ message: 'Failed to update permission', severity: 'error' })
    },
  })

  const unlinkMutation = useMutation({
    mutationFn: (linkId: string) => unlinkGoogleDriveFile(linkId),
    onSuccess: (report) => {
      void queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardGoogleDriveLinks(cardId) })
      const reportMsg = formatRevokeReport(report)
      if (reportMsg) {
        enqueueToast({
          message: reportMsg,
          severity: report.failedCount > 0 ? 'warning' : 'success',
          durationMs: 6000,
        })
      } else {
        enqueueToast({ message: 'File unlinked', severity: 'success' })
      }
    },
    onError: () => {
      enqueueToast({ message: 'Failed to unlink file', severity: 'error' })
    },
  })

  const isConnected = googleStatusQuery.data?.connected ?? false
  const links = linksQuery.data ?? []
  const isBusy = linkFilesMutation.isPending || isPickerLoading

  const handleLinkClick = () => {
    openPicker((files) => {
      const fileIds = files.map(f => f.id)
      if (fileIds.length > 0) {
        linkFilesMutation.mutate(fileIds)
      }
    })
  }

  const sectionContent = (
    <Stack
      spacing={0}
      sx={
        !isConnected
          ? { opacity: 0.5, pointerEvents: 'none', filter: 'grayscale(1)' }
          : undefined
      }
    >
      <Stack direction="row" alignItems="center" sx={{ px: 2, pt: 1.5, pb: 1 }}>
        <GoogleIcon sx={{ fontSize: 16, color: 'primary.main', mr: 0.75, opacity: 0.7 }} />
        <Typography variant="caption" fontWeight={700} color="primary.main" sx={{ letterSpacing: '0.05em', textTransform: 'uppercase', fontSize: '0.6875rem' }}>
          Google Drive
        </Typography>
        <Typography variant="caption" color="text.disabled" sx={{ ml: 0.75, fontSize: '0.6875rem' }}>
          {links.length}
        </Typography>
      </Stack>

      {linksQuery.isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 2 }}>
          <CircularProgress size={24} />
        </Box>
      ) : null}

      {links.length > 0 ? (
        <Stack spacing={0}>
          {links.map((link) => {
            const isLinker = currentUserId !== undefined && link.linkedBy === currentUserId
            const canUnlink = canManageCards && (isLinker || isCardCreator || isProjectManager)
            const canChangePermission = canManageCards && isLinker
            return (
              <GoogleDriveLinkItem
                key={link.id}
                link={link}
                canUnlink={canUnlink}
                onUnlink={(linkId) => unlinkMutation.mutate(linkId)}
                onPermissionChange={canChangePermission ? (linkId, permission) =>
                  updatePermissionMutation.mutate({ linkId, permission }) : undefined}
                isUnlinking={unlinkMutation.isPending}
                isUpdatingPermission={updatePermissionMutation.isPending}
              />
            )
          })}
        </Stack>
      ) : !linksQuery.isLoading ? (
        <Typography variant="caption" color="text.disabled" sx={{ px: 2, pb: 1.5 }}>
          No Google Drive files linked.
        </Typography>
      ) : null}

      {canManageCards ? (
        <Box sx={{ px: 2, pb: 1.5, pt: links.length > 0 ? 1 : 0 }}>
          <Stack direction="row" spacing={1} alignItems="center">
            <Select
              size="small"
              value={sharePermission}
              onChange={(e) => setSharePermission(e.target.value as DriveSharePermission)}
              disabled={!isConnected}
              sx={{ minWidth: 120, '& .MuiSelect-select': { py: 0.5, fontSize: '0.8125rem' } }}
            >
              <MenuItem value="reader">View only</MenuItem>
              <MenuItem value="commenter">Comment</MenuItem>
              <MenuItem value="writer">Edit</MenuItem>
            </Select>
            <Button
              variant="outlined"
              size="small"
              startIcon={isBusy ? <CircularProgress size={16} /> : <AddLinkIcon />}
              onClick={handleLinkClick}
              disabled={!isConnected || isBusy}
            >
              Link File
            </Button>
          </Stack>
        </Box>
      ) : null}
    </Stack>
  )

  if (!isConnected) {
    return (
      <Tooltip title="Connect your Google account in settings to use Google Drive." placement="top">
        <Box sx={{ cursor: 'not-allowed' }}>{sectionContent}</Box>
      </Tooltip>
    )
  }

  return sectionContent
}

export { GoogleDriveLinksSection }
