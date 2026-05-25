/**
 * Board management subsystem.
 *
 * Four end-to-end checks:
 *   - creating a board from the Kanban template seeds To Do / In Progress
 *     / Done columns
 *   - adding a custom column persists across reload
 *   - archiving a column via the inline confirmation removes it from the
 *     board
 *   - the per-board Archive drawer opens from the sidebar with both
 *     Lists and Tasks tabs
 *
 * Out of scope:
 *   - Manage Tags dialog open — low signal; tag CRUD is covered by
 *     component tests.
 *   - Whole-board archive — the dialog exists in the JSX but no UI
 *     control flips its open-state, so there is no driveable end-to-end
 *     path. Card-level archive is exercised in card-management.spec.ts.
 */
import { test, expect } from '../fixtures'
import { ProjectsListPage } from '../pages/ProjectsListPage'
import { ProjectDashboardPage } from '../pages/ProjectDashboardPage'
import { BoardPage } from '../pages/BoardPage'

async function setupBoard(
  signedInPage: import('@playwright/test').Page,
  prefix: string,
): Promise<void> {
  const projectName = `${prefix} Project ${Date.now()}`
  const boardName = `${prefix} Board ${Date.now()}`

  const projectsPage = new ProjectsListPage(signedInPage)
  await projectsPage.goto()
  await projectsPage.createProject(projectName)
  await projectsPage.openProject(projectName)

  const dashboard = new ProjectDashboardPage(signedInPage)
  await dashboard.createBoard(boardName)
  await dashboard.openBoard(boardName)
}

test('create a board from the Kanban template seeds To Do / In Progress / Done', async ({
  signedInPage,
}) => {
  await setupBoard(signedInPage, 'Default Template')

  const board = new BoardPage(signedInPage)
  await expect(board.columnByName('To Do')).toBeVisible()
  await expect(board.columnByName('In Progress')).toBeVisible()
  await expect(board.columnByName('Done')).toBeVisible()
})

test('add a custom column to an existing board, persists across reload', async ({
  signedInPage,
}) => {
  await setupBoard(signedInPage, 'Custom Column')

  const board = new BoardPage(signedInPage)
  const newColumnName = `Review ${Date.now()}`
  await board.addColumn(newColumnName)

  await signedInPage.reload()
  await expect(board.columnByName(newColumnName)).toBeVisible()
})

test('archive a column via the inline confirmation, column disappears from the board', async ({
  signedInPage,
}) => {
  await setupBoard(signedInPage, 'Archive Column')

  const board = new BoardPage(signedInPage)
  const targetColumn = `Temp ${Date.now()}`
  await board.addColumn(targetColumn)
  await expect(board.columnByName(targetColumn)).toBeVisible()

  await signedInPage.getByRole('button', { name: `Archive ${targetColumn}` }).click()
  // The inline confirmation renders as an Alert with a warning "Archive"
  // button — scope to the Alert so we don't collide with the global sidebar
  // "Archive" nav item (which is a button with the same accessible name).
  await signedInPage
    .getByRole('alert')
    .getByRole('button', { name: /^archive$/i })
    .click()

  await expect(board.columnByName(targetColumn)).toHaveCount(0)
})

test('Archive sidebar entry opens the per-board Archive drawer with Lists/Tasks tabs', async ({
  signedInPage,
}) => {
  await setupBoard(signedInPage, 'Archive Drawer')

  // Scope to the sidebar (complementary landmark) and use exact match so we
  // don't collide with the project breadcrumb ("Archive Drawer Project ...")
  // or the toolbar's "Enter multi-archive mode" icon button.
  await signedInPage
    .getByRole('complementary')
    .getByRole('button', { name: 'Archive', exact: true })
    .click()
  await expect(signedInPage.getByRole('heading', { name: 'Board Archive' })).toBeVisible()
  await expect(signedInPage.getByRole('tab', { name: /^lists\s*\(\d+\)/i })).toBeVisible()
  await expect(signedInPage.getByRole('tab', { name: /^tasks\s*\(\d+\)/i })).toBeVisible()
})
