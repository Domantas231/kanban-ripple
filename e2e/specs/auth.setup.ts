import { test as setup } from '@playwright/test'
import { provisionConfirmedUser } from '../support/api'
import { env } from '../support/env'

// Runs once before any chromium / firefox / webkit project.
// Re-creates the primary E2E user so each run starts from a known clean state.
setup('provision primary E2E user', async ({ request }) => {
  await provisionConfirmedUser(request, env.user)
})

// Provision the secondary user used by multi-user / invitation specs.
// Kept independent of the primary user — either may be re-provisioned without
// touching the other.
setup('provision secondary E2E user', async ({ request }) => {
  await provisionConfirmedUser(request, env.otherUser)
})
