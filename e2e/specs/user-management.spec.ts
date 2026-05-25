/**
 * User management subsystem.
 *
 * Five end-to-end checks covering one happy path per flow plus the critical
 * error/gate cases:
 *   - registration happy path (form → "check your email")
 *   - invalid-credentials login error
 *   - logout clears the session AND a protected route then redirects
 *   - auth guard rejects unauthenticated visitors to all protected routes
 *   - password reset via email link (request → follow link → set → re-login)
 *
 * Out of scope here (covered better elsewhere):
 *   - Client-side form validation (weak password, mismatch, email format) →
 *     covered by frontend unit tests on the form components.
 *   - In-app password change, display name, theme toggle → light-touch UI
 *     surfaces; the password-change cycle is exercised by the public reset
 *     flow above.
 *   - Profile photo, Google account linking → require a working blob/file
 *     backend or live Google OAuth.
 */
import { test as baseTest, expect } from '@playwright/test'
import { test } from '../fixtures'
import { LoginPage } from '../pages/LoginPage'
import { RegistrationPage } from '../pages/RegistrationPage'
import { AppShell } from '../pages/AppShell'
import {
  getPasswordResetTokens,
  loginViaApi,
  provisionConfirmedUser,
} from '../support/api'
import { env } from '../support/env'

const STARTING_PASSWORD = 'StartingPass!1'
const NEW_PASSWORD = 'BrandNewPass!2'

baseTest('registration happy path shows the "check your email" confirmation', async ({
  page,
  request,
}) => {
  const uniqueEmail = `e2e-reg-${Date.now()}@kanban.test`
  const registrationPage = new RegistrationPage(page)
  await registrationPage.goto()

  await registrationPage.register(uniqueEmail, 'Strong!Pass1')

  await expect(page.getByRole('heading', { name: /check your email/i })).toBeVisible()
  await expect(page.getByText(uniqueEmail)).toBeVisible()

  await request.post('/api/test/delete-user', { data: { email: uniqueEmail } })
})

baseTest('invalid login credentials show an error and stay on /login', async ({ page }) => {
  const loginPage = new LoginPage(page)
  await loginPage.goto()
  await loginPage.signIn(env.user.email, 'wrong-password')

  await expect(loginPage.errorAlert).toBeVisible()
  await expect(loginPage.errorAlert).toContainText(/invalid credentials/i)
  await expect(page).toHaveURL(/\/login/)
})

test('logout clears session and protected routes redirect to /login', async ({ signedInPage }) => {
  await signedInPage.goto('/projects')
  await expect(
    signedInPage.getByRole('heading', { name: 'Workspaces', exact: true }),
  ).toBeVisible()

  const shell = new AppShell(signedInPage)
  await shell.logout()

  await signedInPage.waitForURL('**/login**')
  await expect(signedInPage.getByRole('heading', { name: /welcome back/i })).toBeVisible()

  await signedInPage.goto('/projects')
  await signedInPage.waitForURL('**/login**')
})

baseTest('auth guard redirects unauthenticated visitors away from protected routes', async ({
  page,
}) => {
  for (const path of ['/projects', '/archive', '/settings']) {
    await page.goto(path)
    await page.waitForURL('**/login**')
    await expect(page).toHaveURL(/\/login/)
  }
  // /projects must also preserve a redirect query param so login can return.
  await page.goto('/projects')
  await page.waitForURL('**/login**')
  await expect(page).toHaveURL(/\/login\?.*redirect=/)
})

baseTest('password reset via email link: request, follow link, set new password, sign in', async ({
  page,
  request,
}) => {
  const email = `pwreset-${Date.now()}@kanban.test`
  await provisionConfirmedUser(request, { email, password: STARTING_PASSWORD })

  try {
    await page.goto('/reset-password')
    await page.getByLabel('Email').fill(email)
    await page.getByRole('button', { name: /send reset link/i }).click()
    await expect(
      page.getByText(/if that email exists then a reset password email was sent/i),
    ).toBeVisible()

    const { encodedToken, encodedEmail } = await getPasswordResetTokens(request, email)
    expect(encodedToken).toBeTruthy()

    await page.goto(
      `/reset-password?token=${encodeURIComponent(encodedToken)}&email=${encodeURIComponent(encodedEmail)}`,
    )
    await expect(page.getByRole('heading', { name: /choose a new password/i })).toBeVisible()

    await page.getByLabel('New password', { exact: true }).fill(NEW_PASSWORD)
    await page.getByLabel('Confirm new password').fill(NEW_PASSWORD)
    await page.getByRole('button', { name: /^reset password$/i }).click()

    await expect(
      page.getByRole('heading', { name: /password reset successful/i }),
    ).toBeVisible()

    // The new password works; the old one is rejected.
    const newLogin = await loginViaApi(request, { email, password: NEW_PASSWORD })
    expect(newLogin.accessToken).toBeTruthy()

    const oldLogin = await request.post(`${env.backendUrl}/api/auth/login`, {
      data: { email, password: STARTING_PASSWORD },
    })
    expect(oldLogin.status()).toBe(401)
  } finally {
    await request.post(`${env.backendUrl}/api/test/delete-user`, { data: { email } })
  }
})
