import { create } from 'zustand'

export type ThemeMode = 'system' | 'light' | 'dark'

export type TagDisplayMode = 'both' | 'name' | 'color'

export type ToastSeverity = 'error' | 'warning' | 'info' | 'success'

export type ToastMessage = {
  id: number
  message: string
  severity: ToastSeverity
  durationMs: number
}

type UiState = {
  activeModal: string | null
  setModal: (modal: string) => void
  closeModal: () => void
  sidebarOpen: boolean
  toggleSidebar: () => void
  sidebarCollapsed: boolean
  toggleSidebarCollapsed: () => void
  mobileDrawerOpen: boolean
  setMobileDrawerOpen: (open: boolean) => void
  activeToast: ToastMessage | null
  enqueueToast: (payload: {
    message: string
    severity?: ToastSeverity
    durationMs?: number
  }) => void
  dismissToast: () => void
  conflictDialogOpen: boolean
  conflictDialogMessage: string
  openConflictDialog: (message?: string) => void
  closeConflictDialog: () => void
  tagDisplayModes: Record<string, TagDisplayMode>
  setTagDisplayMode: (tagId: string, mode: TagDisplayMode) => void
  boardTagDisplayMode: TagDisplayMode
  setBoardTagDisplayMode: (mode: TagDisplayMode) => void
  themeMode: ThemeMode
  setThemeMode: (mode: ThemeMode) => void
  boardArchiveDrawerOpen: boolean
  setBoardArchiveDrawerOpen: (open: boolean) => void
  boardPlannerOpen: boolean
  setBoardPlannerOpen: (open: boolean) => void
  toggleBoardPlanner: () => void
  plannerSidebarCollapsed: boolean
  togglePlannerSidebar: () => void
}

const DEFAULT_TOAST_DURATION_MS = 5000

let nextToastId = 1

export const useUiStore = create<UiState>((set) => ({
  activeModal: null,
  setModal: (modal) => set({ activeModal: modal }),
  closeModal: () => set({ activeModal: null }),
  sidebarOpen: false,
  toggleSidebar: () => set((state) => ({ sidebarOpen: !state.sidebarOpen })),
  sidebarCollapsed: localStorage.getItem('sidebarCollapsed') === 'true',
  toggleSidebarCollapsed: () =>
    set((state) => {
      const next = !state.sidebarCollapsed
      localStorage.setItem('sidebarCollapsed', String(next))
      return { sidebarCollapsed: next }
    }),
  mobileDrawerOpen: false,
  setMobileDrawerOpen: (open) => set({ mobileDrawerOpen: open }),
  activeToast: null,
  enqueueToast: ({ message, severity = 'error', durationMs = DEFAULT_TOAST_DURATION_MS }) => {
    set({
      activeToast: {
        id: nextToastId,
        message,
        severity,
        durationMs,
      },
    })

    nextToastId += 1
  },
  dismissToast: () => {
    set({ activeToast: null })
  },
  conflictDialogOpen: false,
  conflictDialogMessage: 'Conflict detected. Refresh and try again.',
  openConflictDialog: (message) => {
    set({
      conflictDialogOpen: true,
      conflictDialogMessage: message ?? 'Conflict detected. Refresh and try again.',
    })
  },
  closeConflictDialog: () => {
    set({
      conflictDialogOpen: false,
      conflictDialogMessage: 'Conflict detected. Refresh and try again.',
    })
  },
  tagDisplayModes: JSON.parse(localStorage.getItem('tagDisplayModes') ?? '{}') as Record<string, TagDisplayMode>,
  setTagDisplayMode: (tagId, mode) => {
    set((state) => {
      const next = { ...state.tagDisplayModes, [tagId]: mode }
      localStorage.setItem('tagDisplayModes', JSON.stringify(next))
      return { tagDisplayModes: next }
    })
  },
  boardTagDisplayMode: (localStorage.getItem('boardTagDisplayMode') as TagDisplayMode) || 'both',
  setBoardTagDisplayMode: (mode) => {
    localStorage.setItem('boardTagDisplayMode', mode)
    set({ boardTagDisplayMode: mode })
  },
  themeMode: (localStorage.getItem('themeMode') as ThemeMode) || 'system',
  setThemeMode: (mode) => {
    localStorage.setItem('themeMode', mode)
    set({ themeMode: mode })
  },
  boardArchiveDrawerOpen: false,
  setBoardArchiveDrawerOpen: (open) => set({ boardArchiveDrawerOpen: open }),
  boardPlannerOpen: false,
  setBoardPlannerOpen: (open) => set({ boardPlannerOpen: open }),
  toggleBoardPlanner: () => set((state) => ({ boardPlannerOpen: !state.boardPlannerOpen })),
  plannerSidebarCollapsed: false,
  togglePlannerSidebar: () => set((state) => ({ plannerSidebarCollapsed: !state.plannerSidebarCollapsed })),
}))
