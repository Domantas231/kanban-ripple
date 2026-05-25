import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AppErrorBoundary } from './AppErrorBoundary'

let shouldCrash = true

function MaybeCrash() {
  if (shouldCrash) {
    throw new Error('Boom')
  }
  return <p>Recovered</p>
}

describe('AppErrorBoundary', () => {
  beforeEach(() => {
    shouldCrash = true
  })

  it('renders fallback UI and recovers when retry is clicked', async () => {
    const user = userEvent.setup()
    const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

    render(
      <AppErrorBoundary>
        <MaybeCrash />
      </AppErrorBoundary>,
    )

    expect(screen.getByRole('heading', { name: /something went wrong/i })).toBeInTheDocument()
    expect(consoleErrorSpy).toHaveBeenCalled()

    shouldCrash = false
    await user.click(screen.getByRole('button', { name: /try again/i }))

    expect(screen.getByText('Recovered')).toBeInTheDocument()
    consoleErrorSpy.mockRestore()
  })
})
