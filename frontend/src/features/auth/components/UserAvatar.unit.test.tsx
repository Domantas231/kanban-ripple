import { afterEach, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { render, screen, cleanup } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { UserAvatar } from './UserAvatar'
import { server } from '@/testing/msw/server'

afterEach(() => {
  cleanup()
})

function renderAvatar(props: React.ComponentProps<typeof UserAvatar>) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <UserAvatar {...props} />
    </QueryClientProvider>,
  )
}

describe('UserAvatar — initials fallback', () => {
  it('renders the first character of the name uppercased', () => {
    renderAvatar({ userId: 'u-1', name: 'alice' })
    expect(screen.getByText('A')).toBeInTheDocument()
  })

  it('falls back to "?" when no name is supplied', () => {
    renderAvatar({ userId: 'u-2', name: null })
    expect(screen.getByText('?')).toBeInTheDocument()
  })

  it('renders the initial when userId is missing (avatar query disabled)', () => {
    // Without userId the photo query is disabled and never fires.
    renderAvatar({ name: 'bob' })
    expect(screen.getByText('B')).toBeInTheDocument()
  })
})

describe('UserAvatar — photo loading', () => {
  it('still renders the initial fallback when the photo endpoint returns 204', async () => {
    server.use(
      http.get(
        '*/api/auth/users/no-photo-user/profile-photo',
        () => new HttpResponse(new Blob([]), { status: 204 }),
      ),
    )

    renderAvatar({ userId: 'no-photo-user', name: 'gemma' })

    // 204 → null → no <img>; the initial stays visible.
    expect(screen.getByText('G')).toBeInTheDocument()
  })
})
