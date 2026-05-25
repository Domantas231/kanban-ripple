export const authQueryKeys = {
  userProfilePhoto: (userId: string) => ['auth', 'profile-photo', userId] as const,
} as const
