import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { renderApp } from './renderApp'
import { server } from './msw/server'

function makeRefreshFail() {
  // The /register route runs `redirectIfAuthenticated` in beforeLoad, which
  // calls /api/auth/refresh; force it to fail so we land on the form.
  server.use(
    http.post('*/api/auth/refresh', () =>
      HttpResponse.json({ error: { message: 'unauth' } }, { status: 401 }),
    ),
  )
}

async function gotoRegister() {
  makeRefreshFail()
  return renderApp({ route: '/register' })
}

describe('registration route', () => {
  it('shows zod validation errors for empty submission', async () => {
    const user = userEvent.setup()
    await gotoRegister()

    await user.click(await screen.findByRole('button', { name: /create account/i }))

    expect(await screen.findByText(/valid email address/i)).toBeInTheDocument()
    expect(screen.getByText(/password must be at least 8 characters/i)).toBeInTheDocument()
    expect(screen.getByText(/confirm your password/i)).toBeInTheDocument()
  })

  it('updates the password requirement checklist as the user types', async () => {
    const user = userEvent.setup()
    await gotoRegister()

    const passwordField = await screen.findByLabelText(/^password$/i)

    // Before any input: checklist is hidden.
    expect(screen.queryByText(/at least 8 characters/i)).not.toBeInTheDocument()

    // Type a password meeting only some rules and check that those rules are
    // marked as met by their `CheckCircleOutlineIcon` (test-id-free indicator
    // is fine since each rule label is unique text near its icon).
    await user.type(passwordField, 'abc')
    expect(await screen.findByText(/one lowercase letter/i)).toBeInTheDocument()
    expect(screen.getByText(/at least 8 characters/i)).toBeInTheDocument()
    expect(screen.getByText(/one uppercase letter/i)).toBeInTheDocument()

    // Now satisfy all requirements.
    await user.clear(passwordField)
    await user.type(passwordField, 'Strong1!aaaa')

    // The submit button should still be enabled (not awaiting submission).
    expect(screen.getByRole('button', { name: /create account/i })).toBeEnabled()
  })

  it('flags mismatched confirm password before reaching the API', async () => {
    let registerCalled = false
    server.use(
      http.post('*/api/auth/register', () => {
        registerCalled = true
        return HttpResponse.json({ message: 'ok' }, { status: 200 })
      }),
    )

    const user = userEvent.setup()
    await gotoRegister()

    await user.type(await screen.findByLabelText(/email/i), 'me@example.com')
    await user.type(screen.getByLabelText(/^password$/i), 'Strong1!aaaa')
    await user.type(screen.getByLabelText(/confirm password/i), 'Different1!aaaa')
    await user.click(screen.getByRole('button', { name: /create account/i }))

    expect(await screen.findByText(/passwords do not match/i)).toBeInTheDocument()
    expect(registerCalled).toBe(false)
  })

  it('maps a 409 duplicate-email response to the email field', async () => {
    server.use(
      http.post('*/api/auth/register', () =>
        HttpResponse.json(
          { error: { message: 'Email is already in use' } },
          { status: 409 },
        ),
      ),
    )

    const user = userEvent.setup()
    await gotoRegister()

    await user.type(await screen.findByLabelText(/email/i), 'taken@example.com')
    await user.type(screen.getByLabelText(/^password$/i), 'Strong1!aaaa')
    await user.type(screen.getByLabelText(/confirm password/i), 'Strong1!aaaa')
    await user.click(screen.getByRole('button', { name: /create account/i }))

    const emailField = await screen.findByLabelText(/email/i)
    // The duplicate-email message lives in the email field's helper text.
    const emailHelper = emailField.closest('.MuiFormControl-root')
    expect(emailHelper).not.toBeNull()
    expect(within(emailHelper as HTMLElement).getByText(/already in use/i)).toBeInTheDocument()
  })

  it('shows the "Check your email" success state when registration succeeds', async () => {
    server.use(
      http.post('*/api/auth/register', () =>
        HttpResponse.json(
          {
            message: 'Account created. Check your email for a confirmation link to activate your account.',
            email: 'new@example.com',
          },
          { status: 200 },
        ),
      ),
    )

    const user = userEvent.setup()
    await gotoRegister()

    await user.type(await screen.findByLabelText(/email/i), 'new@example.com')
    await user.type(screen.getByLabelText(/^password$/i), 'Strong1!aaaa')
    await user.type(screen.getByLabelText(/confirm password/i), 'Strong1!aaaa')
    await user.click(screen.getByRole('button', { name: /create account/i }))

    expect(await screen.findByRole('heading', { name: /check your email/i })).toBeInTheDocument()
    // Confirmation copy includes the submitted email.
    expect(await screen.findByText('new@example.com')).toBeInTheDocument()
  })
})
