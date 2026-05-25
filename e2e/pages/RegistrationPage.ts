import type { Locator, Page } from '@playwright/test'
import { expect } from '@playwright/test'

export class RegistrationPage {
  readonly page: Page
  readonly emailInput: Locator
  readonly passwordInput: Locator
  readonly confirmPasswordInput: Locator
  readonly submitButton: Locator

  constructor(page: Page) {
    this.page = page
    this.emailInput = page.getByLabel('Email')
    this.passwordInput = page.getByLabel('Password', { exact: true })
    this.confirmPasswordInput = page.getByLabel('Confirm password')
    this.submitButton = page.getByRole('button', { name: /create account/i })
  }

  async goto(): Promise<void> {
    await this.page.goto('/register')
    await expect(this.page.getByRole('heading', { name: /create an account/i })).toBeVisible()
  }

  async register(email: string, password: string): Promise<void> {
    await this.emailInput.fill(email)
    await this.passwordInput.fill(password)
    await this.confirmPasswordInput.fill(password)
    await this.submitButton.click()
  }
}
