import type { Locator, Page } from '@playwright/test'
import { expect } from '@playwright/test'

/**
 * The view at /projects/:projectId — boards live here.
 */
export class ProjectDashboardPage {
  readonly page: Page
  readonly newBoardButton: Locator
  readonly emptyStateCreateButton: Locator
  readonly createDialog: Locator
  readonly createDialogNameInput: Locator
  readonly createDialogSubmit: Locator

  constructor(page: Page) {
    this.page = page
    this.newBoardButton = page.getByRole('button', { name: 'New Board' })
    this.emptyStateCreateButton = page.getByRole('button', { name: 'Create Board' })
    this.createDialog = page.getByRole('dialog')
    this.createDialogNameInput = this.createDialog.getByLabel('Board name')
    this.createDialogSubmit = this.createDialog.getByRole('button', { name: /^create$/i })
  }

  async openCreateDialog(): Promise<void> {
    if (await this.newBoardButton.isVisible().catch(() => false)) {
      await this.newBoardButton.click()
    } else {
      await this.emptyStateCreateButton.click()
    }
    await expect(this.createDialog).toBeVisible()
  }

  async createBoard(name: string): Promise<void> {
    await this.openCreateDialog()
    await this.createDialogNameInput.fill(name)
    // The default selected template is "Kanban" (To Do / In Progress / Done).
    await this.createDialogSubmit.click()
    await expect(this.createDialog).toBeHidden()
  }

  /**
   * Locator for a board card on the dashboard. Anchored on the board-name
   * Typography (exact match) and walks up to the enclosing CardActionArea.
   */
  boardCard(name: string): Locator {
    return this.page.getByText(name, { exact: true }).locator('xpath=ancestor::button[1]')
  }

  async openBoard(name: string): Promise<void> {
    await this.boardCard(name).click()
    await this.page.waitForURL(/\/projects\/[^/]+\/boards\/[^/]+/)
  }
}
