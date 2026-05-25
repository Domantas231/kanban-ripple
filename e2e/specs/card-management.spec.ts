/**
 * Card management subsystem.
 *
 * Five end-to-end checks:
 *   - creating a card from the inline "Add a task" composer; persists across
 *     reload (this also implicitly covers "open detail dialog" in the next
 *     four tests, so a separate "open" test is not needed)
 *   - editing a card's title from the detail dialog and persisting via
 *     "Save Changes"
 *   - posting a comment via the comment composer
 *   - adding a subtask via the inline "+ Add an item" control; staged item
 *     is persisted via "Save Changes" and survives reopen
 *   - archiving a card from the dialog footer ("Archive Task")
 *
 * Out of scope:
 *   - File attachment upload — the dev backend uses NoOpFileStorageService
 *     (see CLAUDE.md), so a real upload + download round-trip is not
 *     exercisable.
 *   - Google Drive attachment — requires a live Google OAuth session.
 */
import { test, expect } from '../fixtures'
import { ProjectsListPage } from '../pages/ProjectsListPage'
import { ProjectDashboardPage } from '../pages/ProjectDashboardPage'
import { BoardPage } from '../pages/BoardPage'

async function setupBoardWithCard(
  signedInPage: import('@playwright/test').Page,
  prefix: string,
): Promise<{ board: BoardPage; cardTitle: string }> {
  const projectName = `${prefix} Project ${Date.now()}`
  const boardName = `${prefix} Board ${Date.now()}`
  const cardTitle = `${prefix} Task ${Date.now()}`

  const projectsPage = new ProjectsListPage(signedInPage)
  await projectsPage.goto()
  await projectsPage.createProject(projectName)
  await projectsPage.openProject(projectName)

  const dashboard = new ProjectDashboardPage(signedInPage)
  await dashboard.createBoard(boardName)
  await dashboard.openBoard(boardName)

  const board = new BoardPage(signedInPage)
  await expect(board.columnByName('To Do')).toBeVisible()
  await board.addCard('To Do', cardTitle)

  return { board, cardTitle }
}

test('add a card via the inline composer; it persists across reload', async ({ signedInPage }) => {
  const { board, cardTitle } = await setupBoardWithCard(signedInPage, 'Create')

  await signedInPage.reload()
  await expect(board.cardByTitle(cardTitle)).toBeVisible()
})

test('edit the card title from the detail dialog and persist via Save Changes', async ({
  signedInPage,
}) => {
  const { board, cardTitle } = await setupBoardWithCard(signedInPage, 'Edit Title')
  const renamedTo = `${cardTitle} (renamed)`

  await board.openCard(cardTitle)

  await signedInPage.getByPlaceholder('Task title').fill(renamedTo)
  await signedInPage.getByRole('button', { name: /save changes/i }).click()

  await expect(board.cardByTitle(renamedTo)).toBeVisible()
  await expect(board.cardByTitle(cardTitle)).toHaveCount(0)
})

test('post a comment from the Card Detail dialog', async ({ signedInPage }) => {
  const { board, cardTitle } = await setupBoardWithCard(signedInPage, 'Comment')

  await board.openCard(cardTitle)

  const commentText = `E2E comment ${Date.now()}`
  await signedInPage.getByPlaceholder('Write a comment...').fill(commentText)
  await signedInPage.getByRole('button', { name: 'Post comment' }).click()

  await expect(signedInPage.getByText(commentText)).toBeVisible()
})

test('add a subtask from the Card Detail dialog and persist it', async ({ signedInPage }) => {
  const { board, cardTitle } = await setupBoardWithCard(signedInPage, 'Subtask')

  await board.openCard(cardTitle)

  await signedInPage.getByRole('button', { name: '+ Add an item' }).click()
  const subtaskDescription = `Subtask ${Date.now()}`
  await signedInPage.getByPlaceholder('New subtask').fill(subtaskDescription)
  await signedInPage.getByRole('button', { name: /^add$/i }).click()

  // Subtasks added inline are staged until the card is saved.
  await signedInPage.getByRole('button', { name: /save changes/i }).click()

  // Reopen the card; the subtask should now be persisted.
  // Subtasks render as a TextField (defaultValue=description), so the
  // description sits in input value rather than visible page text. The
  // adjacent checkbox carries an accessible label that mirrors the
  // description — that's the stable anchor.
  await board.openCard(cardTitle)
  await expect(
    signedInPage.getByLabel(`Toggle subtask ${subtaskDescription}`),
  ).toBeVisible()
})

test('archive a card from the dialog footer; card disappears from the board', async ({
  signedInPage,
}) => {
  const { board, cardTitle } = await setupBoardWithCard(signedInPage, 'Archive')

  await board.openCard(cardTitle)

  await signedInPage.getByRole('button', { name: /^archive task$/i }).click()

  await expect(signedInPage.getByRole('dialog')).toBeHidden()
  await expect(board.cardByTitle(cardTitle)).toHaveCount(0)
})
