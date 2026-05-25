import { test as base, type Page, expect } from '@playwright/test'
import { loginViaApi } from './support/api'
import { env } from './support/env'

type Fixtures = {
  /**
   * A page that already has a valid refresh-token cookie for the primary E2E
   * user. Each test gets its own fresh login, so single-use refresh tokens
   * never collide between parallel tests.
   *
   * Usage:
   *   import { test, expect } from '../fixtures'
   *   test('something', async ({ signedInPage }) => { ... })
   */
  signedInPage: Page
}

export const test = base.extend<Fixtures>({
  signedInPage: async ({ page, context }, use) => {
    // Log in via the API on this browser context so the Set-Cookie response
    // attaches the refresh-token cookie to the same context the page uses.
    await loginViaApi(context.request, env.user)

    await use(page)
  },
})

export { expect }
