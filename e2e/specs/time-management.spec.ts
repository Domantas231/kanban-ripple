/**
 * Time management subsystem.
 *
 * Three end-to-end checks:
 *   - Timeline (Gantt) page renders the empty state for a workspace with no
 *     boards
 *   - Timeline page renders the timeline UI for a workspace with at least
 *     one board (proves the page mounts past the empty-state branch)
 *   - The Planner sidebar entry exists but is gated when the user has not
 *     connected Google Calendar
 *
 * Out of scope:
 *   - Google Calendar synchronisation, drag-to-schedule on the planner
 *     timeline, planner block CRUD — require live Google OAuth that
 *     Playwright cannot drive against the real Google domain.
 *   - Timeline sidebar nav click — trivial routing assertion.
 */
import { test, expect } from '../fixtures'
import { ProjectsListPage } from '../pages/ProjectsListPage'
import { ProjectDashboardPage } from '../pages/ProjectDashboardPage'
import { BoardPage } from '../pages/BoardPage'

test('Timeline page renders heading and "Nothing scheduled" empty state for an empty workspace', async ({
  signedInPage,
}) => {
  const projectName = `Timeline Empty ${Date.now()}`
  const projectsPage = new ProjectsListPage(signedInPage)
  await projectsPage.goto()
  await projectsPage.createProject(projectName)
  await projectsPage.openProject(projectName)

  const projectId = signedInPage.url().match(/\/projects\/([^/?#]+)/)?.[1]
  expect(projectId).toBeTruthy()

  await signedInPage.goto(`/projects/${projectId}/gantt`)
  await expect(signedInPage.getByRole('heading', { name: 'Timeline' })).toBeVisible()
  await expect(signedInPage.getByText(/nothing scheduled/i)).toBeVisible()
})

test('Timeline page renders the timeline UI for a workspace with at least one board', async ({
  signedInPage,
}) => {
  const projectName = `Timeline With Board ${Date.now()}`
  const boardName = `Board ${Date.now()}`

  const projectsPage = new ProjectsListPage(signedInPage)
  await projectsPage.goto()
  await projectsPage.createProject(projectName)
  await projectsPage.openProject(projectName)

  await new ProjectDashboardPage(signedInPage).createBoard(boardName)

  const projectId = signedInPage.url().match(/\/projects\/([^/?#]+)/)?.[1]
  expect(projectId).toBeTruthy()

  await signedInPage.goto(`/projects/${projectId}/gantt`)
  await expect(signedInPage.getByRole('heading', { name: 'Timeline' })).toBeVisible()
  await expect(signedInPage.getByText(/nothing scheduled/i)).toHaveCount(0)
  await expect(signedInPage.getByRole('button', { name: /today/i })).toBeVisible()
})

test('Planner sidebar item is visible but disabled when Google Calendar is not connected', async ({
  signedInPage,
}) => {
  // The Planner item only renders while the user is on a board page.
  const projectName = `Planner Gate ${Date.now()}`
  const boardName = `Sprint ${Date.now()}`

  const projectsPage = new ProjectsListPage(signedInPage)
  await projectsPage.goto()
  await projectsPage.createProject(projectName)
  await projectsPage.openProject(projectName)

  const dashboard = new ProjectDashboardPage(signedInPage)
  await dashboard.createBoard(boardName)
  await dashboard.openBoard(boardName)

  // Confirm the board view actually mounted before we look for the sidebar.
  const board = new BoardPage(signedInPage)
  await expect(board.columnByName('To Do')).toBeVisible()

  // exact: true so we don't collide with project breadcrumbs whose names
  // start with "Planner ..." (the test's project name does).
  const plannerEntry = signedInPage.getByRole('button', { name: 'Planner', exact: true })
  await expect(plannerEntry).toBeVisible()
  await expect(plannerEntry).toHaveAttribute('aria-disabled', 'true')
})
