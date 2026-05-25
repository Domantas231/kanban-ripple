import type { Locator, Page } from '@playwright/test'
import { expect } from '@playwright/test'

/**
 * The board view at /projects/:projectId/boards/:boardId.
 *
 * Selectors here are deliberately conservative: many of the column-/card-
 * level controls in the live UI are non-semantic divs (the "Add list" entry,
 * for example, is a clickable Box rather than a button), so we anchor on
 * stable visible text rather than role+name where the role is not reliable.
 */
export class BoardPage {
  readonly page: Page

  constructor(page: Page) {
    this.page = page
  }

  /**
   * The column wrapper for a column with the given (uppercase-rendered) name.
   * MUI applies `text-transform: uppercase` via CSS, so the underlying text
   * stays original-case and we match exactly on it.
   */
  columnByName(name: string): Locator {
    return this.page.getByText(name, { exact: true })
  }

  /**
   * Click the column-name text to ensure the column is mounted, then click
   * the "Add a task" button scoped to the same column. The "Add a task"
   * button only exists for columns the current user can manage.
   */
  async addCard(columnName: string, title: string): Promise<void> {
    // Use the column header text as an anchor, then walk up to the column
    // wrapper and look for the "Add a task" button inside it. The column
    // wrapper does not have a stable role; ancestor::div with the right
    // children is the most reliable anchor we have without changing the app.
    const header = this.columnByName(columnName)
    const columnRoot = header.locator('xpath=ancestor::div[contains(@class, "MuiBox-root")][1]')

    // Some renders nest deeper; fall back to the page-level Add a task
    // button that's nearest to this column header by clicking the column
    // header's nearest ancestor that contains the Add-a-task button.
    const addButton = columnRoot.getByRole('button', { name: /add a task/i }).first()
    if (await addButton.isVisible().catch(() => false)) {
      await addButton.click()
    } else {
      // Fall back to the first visible "Add a task" button on the page.
      // For boards with one column this is unambiguous.
      await this.page.getByRole('button', { name: /add a task/i }).first().click()
    }

    const titleInput = this.page.getByPlaceholder('Enter a title...')
    await expect(titleInput).toBeVisible()
    await titleInput.fill(title)
    await titleInput.press('Enter')
    // The card's interactive wrapper has aria-label="Open task <title>".
    await expect(this.page.getByLabel(`Open task ${title}`, { exact: true })).toBeVisible()
  }

  cardByTitle(title: string): Locator {
    // exact: true matters: getByLabel is substring by default, so a card
    // titled "Foo" would also match a renamed sibling "Foo (renamed)"
    // through the shared aria-label prefix "Open task Foo".
    return this.page.getByLabel(`Open task ${title}`, { exact: true })
  }

  /**
   * Open the Card Detail dialog for a card by title.
   *
   * The card body has aria-label="Open task <title>" with an onClick that
   * navigates to ?cardId=… (which mounts the dialog). However, the title
   * text inside the card has its own click handler that calls
   * stopPropagation and uses a 250ms timer to disambiguate single-click
   * (enter title-edit mode) from double-click (open dialog). A naive
   * Playwright click() lands on the title text — so the dialog never
   * opens. dblclick() triggers the second-click branch that calls
   * onClick(card) and opens the dialog.
   */
  async openCard(title: string): Promise<void> {
    await this.cardByTitle(title).dblclick()
    await expect(this.page.getByRole('dialog')).toBeVisible()
  }

  /**
   * Add a list/column. The "Add list" entry is a non-semantic Box; we click
   * its visible label, which bubbles to the parent's onClick handler.
   */
  async addColumn(name: string): Promise<void> {
    await this.page.getByText('Add list', { exact: true }).click()
    const input = this.page.getByPlaceholder('List name')
    await expect(input).toBeVisible()
    await input.fill(name)
    await input.press('Enter')
    // The new column header should appear with its name.
    await expect(this.columnByName(name)).toBeVisible()
  }
}
