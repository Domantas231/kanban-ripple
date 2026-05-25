import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { renderApp } from './renderApp'
import { server } from './msw/server'

// Base64url-encoded "user@example.com" — matches what the email would carry
// in the reset link query string.
const ENCODED_EMAIL = 'dXNlckBleGFtcGxlLmNvbQ'

function makeRefreshFail() {
  // /reset-password runs `redirectIfAuthenticated` in beforeLoad — force it
  // to fail so we land on the form instead of being redirected to /projects.
  server.use(
    http.post('*/api/auth/refresh', () =>
      HttpResponse.json({ error: { message: 'unauth' } }, { status: 401 }),
    ),
  )
}

describe('reset-password — request mode (no token)', () => {
  it('shows a zod error when the email is empty', async () => {
    makeRefreshFail()
    const user = userEvent.setup()
    await renderApp({ route: '/reset-password' })

    await user.click(await screen.findByRole('button', { name: /send reset link/i }))
    expect(await screen.findByText(/valid email address/i)).toBeInTheDocument()
  })

  it('shows the generic confirmation message on success (does not leak whether the email exists)', async () => {
    makeRefreshFail()
    const user = userEvent.setup()
    await renderApp({ route: '/reset-password' })

    await user.type(await screen.findByLabelText(/email/i), 'someone@example.com')
    await user.click(screen.getByRole('button', { name: /send reset link/i }))

    expect(
      await screen.findByText(/if that email exists then a reset password email was sent/i),
    ).toBeInTheDocument()
  })

  it('surfaces a server error when the request endpoint fails', async () => {
    makeRefreshFail()
    server.use(
      http.post('*/api/auth/password-reset', () =>
        HttpResponse.json({ error: { message: 'oops' } }, { status: 500 }),
      ),
    )

    const user = userEvent.setup()
    await renderApp({ route: '/reset-password' })

    await user.type(await screen.findByLabelText(/email/i), 'someone@example.com')
    await user.click(screen.getByRole('button', { name: /send reset link/i }))

    expect(await screen.findByText(/unable to process your request/i)).toBeInTheDocument()
  })
})

describe('reset-password — reset mode (with token)', () => {
  function gotoResetForm() {
    makeRefreshFail()
    return renderApp({ route: `/reset-password?token=valid-token&email=${ENCODED_EMAIL}` })
  }

  it('blocks submission when confirm password does not match (and never hits the API)', async () => {
    let resetCalled = false
    server.use(
      http.put('*/api/auth/password-reset', () => {
        resetCalled = true
        return HttpResponse.json({ message: 'ok' }, { status: 200 })
      }),
    )

    const user = userEvent.setup()
    await gotoResetForm()

    await user.type(await screen.findByLabelText(/^new password$/i), 'Strong1!aaaa')
    await user.type(screen.getByLabelText(/confirm new password/i), 'Different1!aaaa')
    await user.click(screen.getByRole('button', { name: /reset password/i }))

    expect(await screen.findByText(/passwords do not match/i)).toBeInTheDocument()
    expect(resetCalled).toBe(false)
  })

  it('shows the success state with a "Sign in" CTA when reset succeeds', async () => {
    server.use(
      http.put('*/api/auth/password-reset', () =>
        HttpResponse.json({ message: 'Password reset successful.' }, { status: 200 }),
      ),
    )

    const user = userEvent.setup()
    await gotoResetForm()

    await user.type(await screen.findByLabelText(/^new password$/i), 'Strong1!aaaa')
    await user.type(screen.getByLabelText(/confirm new password/i), 'Strong1!aaaa')
    await user.click(screen.getByRole('button', { name: /reset password/i }))

    expect(await screen.findByRole('heading', { name: /password reset successful/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /sign in now/i })).toBeInTheDocument()
  })

  it('switches to "Request a new link" when the server reports an expired token', async () => {
    server.use(
      http.put('*/api/auth/password-reset', () =>
        HttpResponse.json(
          { error: { message: 'Reset token has expired.' } },
          { status: 400 },
        ),
      ),
    )

    const user = userEvent.setup()
    await gotoResetForm()

    await user.type(await screen.findByLabelText(/^new password$/i), 'Strong1!aaaa')
    await user.type(screen.getByLabelText(/confirm new password/i), 'Strong1!aaaa')
    await user.click(screen.getByRole('button', { name: /reset password/i }))

    expect(await screen.findByText(/this reset link has expired/i)).toBeInTheDocument()
    // The reset-password button is replaced by a "Request a new link" CTA.
    expect(screen.getByRole('link', { name: /request a new link/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^reset password$/i })).not.toBeInTheDocument()
  })
})
