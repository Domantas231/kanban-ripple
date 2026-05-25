import { http, HttpResponse } from 'msw'

const authResult = {
  userId: '11111111-1111-1111-1111-111111111111',
  email: 'user@example.com',
  accessToken: 'access-token',
  accessTokenExpiresAt: '2030-01-01T00:00:00Z',
  refreshToken: 'refresh-token',
  refreshTokenExpiresAt: '2030-01-08T00:00:00Z',
}

export const handlers = [
  http.post('*/api/auth/register', async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as { email?: string }
    return HttpResponse.json(
      {
        message:
          'Account created. Check your email for a confirmation link to activate your account.',
        email: body.email ?? authResult.email,
      },
      { status: 200 },
    )
  }),
  http.post('*/api/auth/login', () => HttpResponse.json(authResult, { status: 200 })),
  http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })),
  http.get('*/api/projects', () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 25, totalCount: 0 }, { status: 200 }),
  ),
  http.get(/\/api\/boards\/[^/]+\/cards(?:\?.*)?$/, () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 50, totalCount: 0 }, { status: 200 }),
  ),
  http.get('*/api/projects/archived', () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 25, totalCount: 0 }, { status: 200 }),
  ),
  http.post('*/api/auth/password-reset', () =>
    HttpResponse.json({ message: 'Password reset email sent.' }, { status: 200 }),
  ),
  http.put('*/api/auth/password-reset', () =>
    HttpResponse.json({ message: 'Password reset successful.' }, { status: 200 }),
  ),
  http.post('*/api/auth/refresh', () => HttpResponse.json(authResult, { status: 200 })),
  // The profile-photo endpoint is fetched by UserAvatar via Axios with
  // responseType: 'blob'. Returning `new HttpResponse(null, ...)` here
  // surfaces an undici "object.stream is not a function" warning under MSW's
  // XHR interceptor — return an empty blob with 204 to match the Axios path
  // (`status === 204 → null`).
  http.get(
    /\/api\/auth\/users\/[^/]+\/profile-photo$/,
    () => new HttpResponse(new Blob([]), { status: 204 }),
  ),
  http.get('*/api/google/status', () => HttpResponse.json({ connected: false }, { status: 200 })),
  http.get('*/api/favorites', () => HttpResponse.json([], { status: 200 })),
  http.get('*/api/notifications', () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 20, totalCount: 0 }, { status: 200 }),
  ),
  http.get('*/api/notifications/unread-count', () =>
    HttpResponse.json({ count: 0 }, { status: 200 }),
  ),
  http.get(/\/api\/boards\/[^/]+\/subscriptions$/, () => HttpResponse.json([], { status: 200 })),
  http.get(/\/api\/boards\/[^/]+\/tags$/, () => HttpResponse.json([], { status: 200 })),
  http.get(/\/api\/columns\/[^/]+\/subscriptions$/, () => HttpResponse.json([], { status: 200 })),
  http.get(/\/api\/cards\/[^/]+\/subscriptions$/, () => HttpResponse.json([], { status: 200 })),
  http.get(/\/api\/projects\/[^/]+\/subscriptions$/, () => HttpResponse.json([], { status: 200 })),
  // The API function returns a plain array (CardActivity[]); a paginated
  // wrapper here would crash the consumer with `.map is not a function`.
  http.get(/\/api\/cards\/[^/]+\/activities(?:\?.*)?$/, () =>
    HttpResponse.json([], { status: 200 }),
  ),
  http.get(/\/api\/cards\/[^/]+\/google-drive-links$/, () =>
    HttpResponse.json([], { status: 200 }),
  ),
]
