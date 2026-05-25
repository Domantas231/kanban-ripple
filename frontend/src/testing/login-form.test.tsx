import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { renderApp } from './renderApp'
import { server } from './msw/server'

function makeRefreshFail() {
  server.use(
    http.post('*/api/auth/refresh', () =>
      HttpResponse.json({ error: { message: 'unauth' } }, { status: 401 }),
    ),
  )
}

describe('login route', () => {
  it('shows validation errors when submitting an empty form', async () => {
    makeRefreshFail()
    const user = userEvent.setup()
    await renderApp({ route: '/login' })

    const submit = await screen.findByRole('button', { name: /sign in/i })
    await user.click(submit)

    expect(await screen.findByText(/valid email address/i)).toBeInTheDocument()
    expect(screen.getByText(/password is required/i)).toBeInTheDocument()
  })

  it('shows server error message when login fails', async () => {
    makeRefreshFail()
    server.use(
      http.post('*/api/auth/login', () =>
        HttpResponse.json({ error: { message: 'bad' } }, { status: 401 }),
      ),
    )

    const user = userEvent.setup()
    await renderApp({ route: '/login' })

    await user.type(await screen.findByLabelText(/email/i), 'foo@bar.com')
    await user.type(screen.getByLabelText(/password/i), 'wrong')
    await user.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByText(/invalid credentials/i)).toBeInTheDocument()
  })
})
