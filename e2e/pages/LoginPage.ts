import type { Locator, Page } from '@playwright/test'
import { expect } from '@playwright/test'

export class LoginPage {
  readonly page: Page
  readonly emailInput: Locator
  readonly passwordInput: Locator
  readonly submitButton: Locator
  readonly errorAlert: Locator
  readonly registerLink: Locator
  readonly forgotPasswordLink: Locator

  constructor(page: Page) {
    this.page = page
    this.emailInput = page.getByLabel('Email')
    this.passwordInput = page.getByLabel('Password')
    this.submitButton = page.getByRole('button', { name: /sign in/i })
    this.errorAlert = page.getByRole('alert')
    this.registerLink = page.getByRole('link', { name: /create.*account|sign up|register/i })
    this.forgotPasswordLink = page.getByRole('link', { name: /forgot|reset/i })
  }

  async goto(): Promise<void> {
    await this.page.goto('/login')
    await expect(this.page.getByRole('heading', { name: /welcome back/i })).toBeVisible()
  }

  async signIn(email: string, password: string): Promise<void> {
    await this.emailInput.fill(email)
    await this.passwordInput.fill(password)
    await this.submitButton.click()
  }
}
