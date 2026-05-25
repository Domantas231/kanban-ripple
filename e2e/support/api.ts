import type { APIRequestContext } from '@playwright/test'
import { env } from './env'

export type AuthResult = {
  userId: string
  email: string
  userName: string | null
  accessToken: string
  expiresAtUtc: string
}

export type TestUser = {
  email: string
  password: string
}

/**
 * Provisions a confirmed user via the backend API. Idempotent — safe to call
 * before every test run. Uses the dev-only /api/test/* endpoints.
 *
 * The flow:
 *   1. Best-effort delete the existing user (cleans owned projects + tokens).
 *   2. Register a fresh user.
 *   3. Confirm the email via the test-only endpoint so the user can log in.
 */
export async function provisionConfirmedUser(
  request: APIRequestContext,
  user: TestUser,
): Promise<void> {
  const deleteResponse = await request.post(`${env.backendUrl}/api/test/delete-user`, {
    data: { email: user.email },
  })
  if (!deleteResponse.ok()) {
    throw new Error(
      `Failed to delete user ${user.email} (status ${deleteResponse.status()}): ${await deleteResponse.text()}`,
    )
  }

  const registerResponse = await request.post(`${env.backendUrl}/api/auth/register`, {
    data: { email: user.email, password: user.password },
  })
  if (!registerResponse.ok()) {
    throw new Error(
      `Failed to register ${user.email} (status ${registerResponse.status()}): ${await registerResponse.text()}`,
    )
  }

  const confirmResponse = await request.post(`${env.backendUrl}/api/test/confirm-email`, {
    data: { email: user.email },
  })
  if (!confirmResponse.ok()) {
    throw new Error(
      `Failed to confirm email for ${user.email} (status ${confirmResponse.status()}): ${await confirmResponse.text()}`,
    )
  }
}

/**
 * Logs the user in via the API and returns the access token + cookies.
 * Used when a test needs a fresh session that doesn't match the storage state.
 */
export async function loginViaApi(
  request: APIRequestContext,
  user: TestUser,
): Promise<AuthResult> {
  const response = await request.post(`${env.backendUrl}/api/auth/login`, {
    data: { email: user.email, password: user.password },
  })
  if (!response.ok()) {
    throw new Error(
      `Login failed for ${user.email} (status ${response.status()}): ${await response.text()}`,
    )
  }
  return (await response.json()) as AuthResult
}

/**
 * Reads the active password-reset token for a user via the dev-only test
 * endpoint and returns the values needed to build a reset URL. Throws if no
 * token is currently active (you must call POST /api/auth/password-reset
 * first to materialize one).
 */
export async function getPasswordResetTokens(
  request: APIRequestContext,
  email: string,
): Promise<{ encodedToken: string; encodedEmail: string }> {
  const response = await request.get(
    `${env.backendUrl}/api/test/password-reset-token?email=${encodeURIComponent(email)}`,
  )
  if (!response.ok()) {
    throw new Error(
      `Failed to fetch password-reset token for ${email} (status ${response.status()}): ${await response.text()}`,
    )
  }
  return (await response.json()) as { encodedToken: string; encodedEmail: string }
}

/**
 * Reads the most recent unaccepted invitation token for a (project, email)
 * pair via the dev-only test endpoint. Throws if no active invitation exists.
 */
export async function getInvitationToken(
  request: APIRequestContext,
  projectId: string,
  email: string,
): Promise<string> {
  const response = await request.get(
    `${env.backendUrl}/api/test/invitation-token?projectId=${projectId}&email=${encodeURIComponent(email)}`,
  )
  if (!response.ok()) {
    throw new Error(
      `Failed to fetch invitation token for ${email} on project ${projectId} (status ${response.status()}): ${await response.text()}`,
    )
  }
  const body = (await response.json()) as { token: string; expiresAt: string }
  return body.token
}

/**
 * Creates a project on behalf of the signed-in user. Used by tests that want
 * a project to exist without driving the UI for setup.
 */
export async function createProjectViaApi(
  request: APIRequestContext,
  accessToken: string,
  name: string,
): Promise<{ id: string; name: string }> {
  const response = await request.post(`${env.backendUrl}/api/projects`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: { name },
  })
  if (!response.ok()) {
    throw new Error(
      `Failed to create project (status ${response.status()}): ${await response.text()}`,
    )
  }
  return (await response.json()) as { id: string; name: string }
}

/**
 * Sends a workspace invitation to the given email on behalf of the signed-in
 * user. The token can then be retrieved via {@link getInvitationToken}.
 */
export async function inviteUserViaApi(
  request: APIRequestContext,
  accessToken: string,
  projectId: string,
  email: string,
): Promise<void> {
  const response = await request.post(
    `${env.backendUrl}/api/projects/${projectId}/invite`,
    {
      headers: { Authorization: `Bearer ${accessToken}` },
      data: { email },
    },
  )
  if (!response.ok()) {
    throw new Error(
      `Failed to invite ${email} to project ${projectId} (status ${response.status()}): ${await response.text()}`,
    )
  }
}
