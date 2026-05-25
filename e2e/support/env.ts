function required(name: string, fallback?: string): string {
  const value = process.env[name] ?? fallback
  if (!value) {
    throw new Error(`Missing required env var: ${name}`)
  }
  return value
}

export const env = {
  frontendUrl: required('E2E_FRONTEND_URL', 'http://localhost:5173'),
  backendUrl: required('E2E_BACKEND_URL', 'http://localhost:5231'),
  user: {
    email: required('E2E_USER_EMAIL', 'e2e-user@kanban.test'),
    password: required('E2E_USER_PASSWORD', 'E2eUserPass!2024'),
  },
  otherUser: {
    email: required('E2E_OTHER_EMAIL', 'e2e-other@kanban.test'),
    password: required('E2E_OTHER_PASSWORD', 'E2eOtherPass!2024'),
  },
} as const
