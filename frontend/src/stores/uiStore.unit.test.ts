import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { useUiStore } from './uiStore'

const initial = useUiStore.getState()

function resetStore(): void {
  useUiStore.setState({
    activeModal: null,
    sidebarOpen: false,
    sidebarCollapsed: false,
    mobileDrawerOpen: false,
    activeToast: null,
    conflictDialogOpen: false,
    conflictDialogMessage: 'Conflict detected. Refresh and try again.',
    boardArchiveDrawerOpen: false,
    boardPlannerOpen: false,
    plannerSidebarCollapsed: false,
  })
}

beforeEach(() => {
  resetStore()
  localStorage.clear()
})

afterEach(() => {
  // Restore actions in case any test reaches into setState beyond data fields.
  useUiStore.setState(initial, false)
})

describe('uiStore — toasts', () => {
  it('starts with no active toast', () => {
    expect(useUiStore.getState().activeToast).toBeNull()
  })

  it('enqueueToast applies default severity "error" and the default duration', () => {
    useUiStore.getState().enqueueToast({ message: 'oops' })
    const toast = useUiStore.getState().activeToast
    expect(toast).not.toBeNull()
    expect(toast!.message).toBe('oops')
    expect(toast!.severity).toBe('error')
    expect(toast!.durationMs).toBe(5000)
  })

  it('enqueueToast respects explicit severity and duration', () => {
    useUiStore.getState().enqueueToast({ message: 'rate limited', severity: 'warning', durationMs: 7000 })
    const toast = useUiStore.getState().activeToast!
    expect(toast.severity).toBe('warning')
    expect(toast.durationMs).toBe(7000)
  })

  it('assigns a unique id to each enqueued toast', () => {
    useUiStore.getState().enqueueToast({ message: 'first' })
    const firstId = useUiStore.getState().activeToast!.id
    useUiStore.getState().enqueueToast({ message: 'second' })
    const secondId = useUiStore.getState().activeToast!.id
    expect(secondId).not.toBe(firstId)
  })

  it('dismissToast clears the active toast', () => {
    useUiStore.getState().enqueueToast({ message: 'oops' })
    useUiStore.getState().dismissToast()
    expect(useUiStore.getState().activeToast).toBeNull()
  })
})

describe('uiStore — conflict dialog', () => {
  it('opens with a default message when none is provided', () => {
    useUiStore.getState().openConflictDialog()
    const state = useUiStore.getState()
    expect(state.conflictDialogOpen).toBe(true)
    expect(state.conflictDialogMessage).toMatch(/refresh/i)
  })

  it('opens with a custom message', () => {
    useUiStore.getState().openConflictDialog('Custom conflict copy')
    expect(useUiStore.getState().conflictDialogMessage).toBe('Custom conflict copy')
  })

  it('closeConflictDialog hides the dialog and resets to the default message', () => {
    useUiStore.getState().openConflictDialog('custom')
    useUiStore.getState().closeConflictDialog()
    const state = useUiStore.getState()
    expect(state.conflictDialogOpen).toBe(false)
    expect(state.conflictDialogMessage).toMatch(/refresh/i)
  })
})

describe('uiStore — sidebar and drawers', () => {
  it('toggleSidebar flips sidebarOpen', () => {
    useUiStore.getState().toggleSidebar()
    expect(useUiStore.getState().sidebarOpen).toBe(true)
    useUiStore.getState().toggleSidebar()
    expect(useUiStore.getState().sidebarOpen).toBe(false)
  })

  it('toggleSidebarCollapsed persists to localStorage', () => {
    useUiStore.getState().toggleSidebarCollapsed()
    expect(useUiStore.getState().sidebarCollapsed).toBe(true)
    expect(localStorage.getItem('sidebarCollapsed')).toBe('true')

    useUiStore.getState().toggleSidebarCollapsed()
    expect(useUiStore.getState().sidebarCollapsed).toBe(false)
    expect(localStorage.getItem('sidebarCollapsed')).toBe('false')
  })

  it('setMobileDrawerOpen sets the drawer state directly', () => {
    useUiStore.getState().setMobileDrawerOpen(true)
    expect(useUiStore.getState().mobileDrawerOpen).toBe(true)
    useUiStore.getState().setMobileDrawerOpen(false)
    expect(useUiStore.getState().mobileDrawerOpen).toBe(false)
  })

  it('toggleBoardPlanner flips boardPlannerOpen independently of setBoardPlannerOpen', () => {
    useUiStore.getState().toggleBoardPlanner()
    expect(useUiStore.getState().boardPlannerOpen).toBe(true)
    useUiStore.getState().setBoardPlannerOpen(false)
    expect(useUiStore.getState().boardPlannerOpen).toBe(false)
  })
})

describe('uiStore — modal and theme/tag persistence', () => {
  it('setModal / closeModal manage activeModal', () => {
    useUiStore.getState().setModal('create-project')
    expect(useUiStore.getState().activeModal).toBe('create-project')
    useUiStore.getState().closeModal()
    expect(useUiStore.getState().activeModal).toBeNull()
  })

  it('setTagDisplayMode persists per-tag display modes to localStorage', () => {
    useUiStore.getState().setTagDisplayMode('tag-1', 'name')
    useUiStore.getState().setTagDisplayMode('tag-2', 'color')

    const stored = JSON.parse(localStorage.getItem('tagDisplayModes') ?? '{}') as Record<string, string>
    expect(stored).toEqual({ 'tag-1': 'name', 'tag-2': 'color' })
    expect(useUiStore.getState().tagDisplayModes).toEqual({ 'tag-1': 'name', 'tag-2': 'color' })
  })

  it('setBoardTagDisplayMode persists the global default', () => {
    useUiStore.getState().setBoardTagDisplayMode('color')
    expect(useUiStore.getState().boardTagDisplayMode).toBe('color')
    expect(localStorage.getItem('boardTagDisplayMode')).toBe('color')
  })

  it('setThemeMode persists the chosen mode', () => {
    useUiStore.getState().setThemeMode('dark')
    expect(useUiStore.getState().themeMode).toBe('dark')
    expect(localStorage.getItem('themeMode')).toBe('dark')
  })
})
