import LinkOffIcon from '@mui/icons-material/LinkOff'
import AssignmentIcon from '@mui/icons-material/Assignment'
import DescriptionIcon from '@mui/icons-material/Description'
import FolderIcon from '@mui/icons-material/Folder'
import InsertDriveFileIcon from '@mui/icons-material/InsertDriveFile'
import LanguageIcon from '@mui/icons-material/Language'
import ShortcutIcon from '@mui/icons-material/Shortcut'
import SlideshowIcon from '@mui/icons-material/Slideshow'
import TableChartIcon from '@mui/icons-material/TableChart'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import ListItemText from '@mui/material/ListItemText'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import Stack from '@mui/material/Stack'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { useState } from 'react'
import type { DriveSharePermission, GoogleDriveLink } from '@/lib/types'

const permissionLabels: Record<DriveSharePermission, string> = {
  reader: 'View only',
  commenter: 'Comment',
  writer: 'Edit',
}

const permissionOptions: DriveSharePermission[] = ['reader', 'commenter', 'writer']

type GoogleDriveLinkItemProps = {
  link: GoogleDriveLink
  canUnlink: boolean
  onUnlink: (linkId: string) => void
  onPermissionChange?: (linkId: string, permission: DriveSharePermission) => void
  isUnlinking?: boolean
  isUpdatingPermission?: boolean
}

function formatDate(iso: string): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return iso
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function getFileIcon(mimeType: string) {
  switch (mimeType) {
    case 'application/vnd.google-apps.document':
      return <DescriptionIcon fontSize="small" />
    case 'application/vnd.google-apps.spreadsheet':
      return <TableChartIcon fontSize="small" />
    case 'application/vnd.google-apps.presentation':
      return <SlideshowIcon fontSize="small" />
    case 'application/vnd.google-apps.form':
      return <AssignmentIcon fontSize="small" />
    case 'application/vnd.google-apps.site':
      return <LanguageIcon fontSize="small" />
    case 'application/vnd.google-apps.folder':
      return <FolderIcon fontSize="small" />
    case 'application/vnd.google-apps.shortcut':
      return <ShortcutIcon fontSize="small" />
    default:
      return <InsertDriveFileIcon fontSize="small" />
  }
}

function GoogleDriveLinkItem({ link, canUnlink, onUnlink, onPermissionChange, isUnlinking, isUpdatingPermission }: GoogleDriveLinkItemProps) {
  const [permMenuAnchor, setPermMenuAnchor] = useState<HTMLElement | null>(null)

  const handleUnlinkClick = () => {
    onUnlink(link.id)
  }

  const sizeAndDate = link.googleModifiedAt ? `Modified ${formatDate(link.googleModifiedAt)}` : null

  return (
    <Box
      component="a"
      href={link.webViewLink}
      target="_blank"
      rel="noopener noreferrer"
      sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 1.5,
        px: 2,
        py: 1.25,
        overflow: 'hidden',
        textDecoration: 'none',
        color: 'inherit',
        cursor: 'pointer',
        '&:hover': { bgcolor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.06)' : 'rgba(13, 148, 136, 0.04)' },
        '&:hover .drive-actions': { opacity: 1 },
      }}
    >
      <Box sx={{ color: 'text.disabled', display: 'flex', flexShrink: 0 }}>
        {getFileIcon(link.mimeType)}
      </Box>

      <Box sx={{ flex: 1, minWidth: 0, overflow: 'hidden', display: 'flex', flexDirection: 'column', justifyContent: 'space-between', alignSelf: 'stretch' }}>
        <Typography variant="caption" noWrap sx={{ color: 'text.primary', fontWeight: 500, display: 'block' }}>
          {link.name}
        </Typography>
        <Box>
          {sizeAndDate ? (
            <Typography variant="caption" color="text.disabled" sx={{ fontSize: '0.625rem', display: 'block' }}>
              {sizeAndDate}
            </Typography>
          ) : null}
          <Typography variant="caption" color="text.disabled" sx={{ fontSize: '0.625rem', display: 'block' }}>
            Linked by {link.linkedByUserName}
          </Typography>
        </Box>
      </Box>

      <Stack alignItems="flex-end" justifyContent="space-between" spacing={1} onClick={(e) => e.preventDefault()} sx={{ flexShrink: 0, alignSelf: 'stretch' }}>
        {onPermissionChange ? (
          <>
            <Typography
              component="span"
              variant="caption"
              onClick={(e) => setPermMenuAnchor(e.currentTarget)}
              sx={{
                fontSize: '0.625rem',
                border: 1,
                borderColor: 'divider',
                borderRadius: 0.5,
                px: 0.75,
                py: 0.25,
                lineHeight: '16px',
                cursor: 'pointer',
                whiteSpace: 'nowrap',
                opacity: isUpdatingPermission ? 0.5 : 1,
                '&:hover': { bgcolor: 'action.hover' },
              }}
            >
              {permissionLabels[link.sharePermission] ?? 'View only'}
            </Typography>
            <Menu
              anchorEl={permMenuAnchor}
              open={Boolean(permMenuAnchor)}
              onClose={() => setPermMenuAnchor(null)}
              slotProps={{ paper: { sx: { minWidth: 120 } } }}
            >
              {permissionOptions.map((perm) => (
                <MenuItem
                  key={perm}
                  selected={perm === link.sharePermission}
                  onClick={() => {
                    setPermMenuAnchor(null)
                    if (perm !== link.sharePermission) {
                      onPermissionChange(link.id, perm)
                    }
                  }}
                >
                  <ListItemText>{permissionLabels[perm]}</ListItemText>
                </MenuItem>
              ))}
            </Menu>
          </>
        ) : (
          <Typography
            component="span"
            variant="caption"
            sx={{
              fontSize: '0.625rem',
              border: 1,
              borderColor: 'divider',
              borderRadius: 0.5,
              px: 0.75,
              py: 0.25,
              lineHeight: '16px',
              whiteSpace: 'nowrap',
            }}
          >
            {permissionLabels[link.sharePermission] ?? 'View only'}
          </Typography>
        )}
        {canUnlink ? (
          <Box className="drive-actions" sx={{ opacity: 0 }}>
            <Tooltip title="Unlink file">
              <IconButton
                size="small"
                onClick={handleUnlinkClick}
                disabled={isUnlinking}
                aria-label={`Unlink ${link.name}`}
                color="error"
                sx={{ p: 0.5 }}
              >
                <LinkOffIcon sx={{ fontSize: 16 }} />
              </IconButton>
            </Tooltip>
          </Box>
        ) : null}
      </Stack>
    </Box>
  )
}

export { GoogleDriveLinkItem }
