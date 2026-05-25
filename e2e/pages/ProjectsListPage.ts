import type { Locator, Page } from '@playwright/test'
import { expect } from '@playwright/test'

export class ProjectsListPage {
  readonly page: Page
  readonly heading: Locator
  readonly searchInput: Locator
  readonly newWorkspaceButton: Locator
  readonly emptyStateCreateButton: Locator
  readonly createDialog: Locator
  readonly createDialogNameInput: Locator
  readonly createDialogSubmit: Locator

  constructor(page: Page) {
    this.page = page
    this.heading = page.getByRole('heading', { name: 'Workspaces', exact: true })
    this.searchInput = page.getByPlaceholder('Search workspaces...')
    this.newWorkspaceButton = page.getByRole('button', { name: 'New Workspace' })
    this.emptyStateCreateButton = page.getByRole('button', { name: 'Create Workspace' })
    this.createDialog = page.getByRole('dialog')
    this.createDialogNameInput = this.createDialog.getByLabel('Workspace name')
    this.createDialogSubmit = this.createDialog.getByRole('button', { name: /^create$/i })
  }

  async goto(): Promise<void> {
    await this.page.goto('/projects')
    await expect(this.heading).toBeVisible()
  }

  async openCreateDialog(): Promise<void> {
    // Use whichever entry is currently visible: the empty-state CTA appears
    // when there are no projects, the toolbar button when there are.
    if (await this.newWorkspaceButton.isVisible().catch(() => false)) {
      await this.newWorkspaceButton.click()
    } else {
      await this.emptyStateCreateButton.click()
    }
    await expect(this.createDialog).toBeVisible()
  }

  async createProject(name: string): Promise<void> {
    await this.openCreateDialog()
    await this.createDialogNameInput.fill(name)
    await this.createDialogSubmit.click()
    await expect(this.createDialog).toBeHidden()
  }

  projectCard(name: string): Locator {
    // Anchor on the project-name Typography (exact match), then walk up to
    // the enclosing CardActionArea (role="button"). A `hasText` filter would
    // also match cards whose name is a substring of another workspace's name.
    return this.page.getByText(name, { exact: true }).locator('xpath=ancestor::button[1]')
  }

  async openProject(name: string): Promise<void> {
    await this.projectCard(name).click()
    await this.page.waitForURL(/\/projects\/[^/]+/)
  }

  async search(query: string): Promise<void> {
    await this.searchInput.fill(query)
  }
}
