import type { Locator, Page } from '@playwright/test'

export class AppShell {
  readonly page: Page
  readonly logoutButton: Locator
  readonly mobileMenuButton: Locator

  constructor(page: Page) {
    this.page = page
    this.logoutButton = page.getByRole('button', { name: 'Logout', exact: true })
    this.mobileMenuButton = page.getByRole('button', { name: 'Open navigation' })
  }

  async logout(): Promise<void> {
    await this.logoutButton.click()
  }
}
