import { forwardRef } from 'react'
import { useQuery } from '@tanstack/react-query'
import Avatar from '@mui/material/Avatar'
import type { SxProps, Theme } from '@mui/material/styles'
import { getUserProfilePhoto } from '../api/auth'
import { authQueryKeys } from '../api/query-keys'

type UserAvatarProps = {
  userId?: string | null
  name?: string | null
  sx?: SxProps<Theme>
}

function getInitials(name?: string | null): string {
  if (!name) return '?'
  return name.charAt(0).toUpperCase()
}

const UserAvatar = forwardRef<HTMLDivElement, UserAvatarProps>(
  function UserAvatar({ userId, name, sx }, ref) {
    const photoQuery = useQuery({
      queryKey: authQueryKeys.userProfilePhoto(userId ?? ''),
      queryFn: () => getUserProfilePhoto(userId!),
      enabled: Boolean(userId),
      staleTime: 5 * 60 * 1000,
      gcTime: 10 * 60 * 1000,
    })

    return (
      <Avatar
        ref={ref}
        src={photoQuery.data ?? undefined}
        sx={[
          {
            border: '1px solid',
            borderColor: 'common.white',
            boxSizing: 'border-box',
          },
          ...(Array.isArray(sx) ? sx : [sx]),
        ]}
      >
        {getInitials(name)}
      </Avatar>
    )
  },
)

export { UserAvatar }
