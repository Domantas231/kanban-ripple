/**
 * Workspace management subsystem.
 *
 * Five end-to-end checks:
 *   - create workspace, see it in the list, search filters it, open it
 *     (one merged journey — exercises the whole list view)
 *   - rename a workspace from the settings page
 *   - multi-user invitation accept flow (the most valuable workspace test —
 *     proves email-less invitation tokens work end-to-end across two users)
 *   - global search dialog opens, accepts a query, closes on Escape
 *   - manager+ role can reach the workspace overview page (/swimlane)
 *
 * Out of scope:
 *   - "Send invitation from settings" — the multi-user accept test below
 *     already exercises the invite endpoint and round-trips a token.
 *   - Archive-cancel and unauth/missing-token edge cases — defensive UI
 *     paths covered better by component-level tests.
 *   - Archive page contents — covered by the archive feature's own tests.
 */
import { test, expect } from '../fixtures'
import { test as baseTest } from '@playwright/test'
import { ProjectsListPage } from '../pages/ProjectsListPage'
import {
  createProjectViaApi,
  getInvitationToken,
  inviteUserViaApi,
  loginViaApi,
} from '../support/api'
import { env } from '../support/env'

test('create a workspace, search filters it, open it from the list', async ({ signedInPage }) => {
  const uniqueName = `E2E Workspace ${Date.now()}`
  const projectsPage = new ProjectsListPage(signedInPage)

  await projectsPage.goto()
  await projectsPage.createProject(uniqueName)

  const card = projectsPage.projectCard(uniqueName)
  await expect(card).toBeVisible()

  await projectsPage.search('zzz-no-match')
  await expect(projectsPage.projectCard(uniqueName)).toHaveCount(0)
  await projectsPage.searchInput.fill('')

  await projectsPage.openProject(uniqueName)
  await expect(signedInPage).toHaveURL(/\/projects\/[0-9a-f-]{36}/)
  await expect(signedInPage.getByRole('heading', { name: uniqueName })).toBeVisible()
})

test('rename a workspace from settings, see new name on the detail page', async ({
  signedInPage,
}) => {
  const initialName = `Rename E2E ${Date.now()}`
  const renamedTo = `${initialName} (renamed)`

  const projectsPage = new ProjectsListPage(signedInPage)
  await projectsPage.goto()
  await projectsPage.createProject(initialName)
  await projectsPage.openProject(initialName)

  const projectId = signedInPage.url().match(/\/projects\/([^/?#]+)/)?.[1]
  expect(projectId).toBeTruthy()

  await signedInPage.goto(`/projects/${projectId}/settings`)
  await expect(signedInPage.getByRole('heading', { name: 'General Details' })).toBeVisible()

  await signedInPage.getByPlaceholder('Enter workspace name').fill(renamedTo)

  // The save mutation is async — if we navigate immediately, the in-flight
  // PUT gets cancelled by the goto and the rename is lost. Wait for the
  // response before navigating away.
  const updateResponse = signedInPage.waitForResponse(
    (response) =>
      response.url().endsWith(`/api/projects/${projectId}`) &&
      response.request().method() === 'PUT',
  )
  await signedInPage.getByRole('button', { name: /save changes/i }).click()
  await updateResponse

  await signedInPage.goto(`/projects/${projectId}`)
  await expect(signedInPage.getByRole('heading', { name: renamedTo })).toBeVisible()
})

baseTest('owner invites another user, who accepts via the invitation link', async ({
  browser,
  request,
}) => {
  const projectName = `Invite E2E ${Date.now()}`

  // Owner side: API-only setup — log in, create a project, send invite.
  const ownerAuth = await loginViaApi(request, env.user)
  const project = await createProjectViaApi(request, ownerAuth.accessToken, projectName)
  await inviteUserViaApi(request, ownerAuth.accessToken, project.id, env.otherUser.email)
  const invitationToken = await getInvitationToken(request, project.id, env.otherUser.email)
  expect(invitationToken).toBeTruthy()

  // Invitee side: fresh browser context, log in, visit accept URL.
  const inviteeContext = await browser.newContext()
  try {
    await loginViaApi(inviteeContext.request, env.otherUser)
    const inviteePage = await inviteeContext.newPage()
    await inviteePage.goto(`/invitations/accept?token=${encodeURIComponent(invitationToken)}`)

    await expect(inviteePage.getByText(/invitation accepted/i)).toBeVisible()

    const projectsPage = new ProjectsListPage(inviteePage)
    await projectsPage.goto()
    await expect(projectsPage.projectCard(projectName)).toBeVisible()
  } finally {
    await inviteeContext.close()
  }
})

test('global search dialog opens, accepts a query, and closes on Escape', async ({
  signedInPage,
}) => {
  const projectName = `Search Target ${Date.now()}`
  const projectsPage = new ProjectsListPage(signedInPage)
  await projectsPage.goto()
  await projectsPage.createProject(projectName)

  await signedInPage.goto('/projects')
  await signedInPage.getByRole('button', { name: /^search\.\.\.$/i }).click()

  const searchInput = signedInPage.getByPlaceholder('Search workspaces, boards, tasks...')
  await expect(searchInput).toBeVisible()
  await searchInput.fill(projectName)

  await searchInput.press('Escape')
  await expect(searchInput).toBeHidden()
})

test('manager+ role can reach the workspace overview page', async ({ signedInPage }) => {
  // The signed-in user is the workspace owner (role = Owner ≥ Manager), so
  // /swimlane is reachable.
  const projectName = `Overview ${Date.now()}`
  const projectsPage = new ProjectsListPage(signedInPage)
  await projectsPage.goto()
  await projectsPage.createProject(projectName)
  await projectsPage.openProject(projectName)

  const projectId = signedInPage.url().match(/\/projects\/([^/?#]+)/)?.[1]
  expect(projectId).toBeTruthy()

  await signedInPage.goto(`/projects/${projectId}/swimlane`)
  await expect(signedInPage.getByRole('heading', { name: 'Overview' })).toBeVisible()
})
