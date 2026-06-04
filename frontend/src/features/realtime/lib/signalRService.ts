import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import type { Card } from '@/lib/types'
import { queryClient } from '@/lib/query-client'
import { boardsQueryKeys } from '@/features/boards'
import { cardsQueryKeys } from '@/features/cards'
import { projectsQueryKeys } from '@/features/projects'
import { notificationsQueryKeys } from '@/features/notifications'
import { useAuthStore } from '@/features/auth'
import { useRealtimeStore } from '@/features/realtime/stores/realtimeStore'

function resolveSignalRHubUrl(): string {
  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  return `${apiBaseUrl.replace(/\/$/, '')}/hubs/project`
}

function isTestEnvironment(): boolean {
  return Boolean(import.meta.env.VITEST)
}

function isSignalRDisabledForEnvironment(): boolean {
  if (!isTestEnvironment()) {
    return false
  }

  return (
    (globalThis as { __KANBAN_ENABLE_SIGNALR_TESTS?: boolean }).__KANBAN_ENABLE_SIGNALR_TESTS !==
    true
  )
}

class SignalRService {
  private readonly connection: HubConnection | null
  private startPromise: Promise<void> | null = null
  private stopPromise: Promise<void> | null = null

  constructor() {
    if (isSignalRDisabledForEnvironment()) {
      this.connection = null
      return
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(resolveSignalRHubUrl(), {
        accessTokenFactory: () => useAuthStore.getState().accessToken ?? '',
      })
      .withAutomaticReconnect([1000, 2000, 4000, 8000, 16000, 30000])
      .configureLogging(LogLevel.Warning)
      .build()

    this.connection.onreconnecting(() => {
      useRealtimeStore.getState().setConnectionState('reconnecting')
    })

    this.connection.onreconnected(() => {
      useRealtimeStore.getState().setConnectionState('connected')
    })

    this.connection.onclose(() => {
      useRealtimeStore.getState().setConnectionState('disconnected')
    })

    this.registerEventHandlers()
  }

  private registerEventHandlers(): void {
    const connection = this.connection
    if (!connection) {
      return
    }

    const registerAliases = (
      eventNames: readonly string[],
      handler: (...args: never[]) => void,
    ) => {
      for (const eventName of eventNames) {
        connection.on(eventName, handler)
      }
    }

    const invalidateCardQueriesForBoard = (card: Card) => {
      const boardId = card.column?.boardId
      if (boardId) {
        void queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boardCards(boardId) })
      } else {
        void queryClient.invalidateQueries({
          predicate: (query) => query.queryKey.some((segment) => segment === 'cards'),
        })
      }
      void queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(card.id) })
      void queryClient.invalidateQueries({
        queryKey: cardsQueryKeys.cardGoogleDriveLinks(card.id),
      })
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey.some((segment) => segment === 'swimlane'),
      })
    }

    const invalidateCardQueriesBroad = () => {
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey.some((segment) => segment === 'cards'),
      })
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey.some((segment) => segment === 'swimlane'),
      })
    }

    const invalidateColumnQueries = () => {
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey.some((segment) => segment === 'columns'),
      })

      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey.some((segment) => segment === 'swimlane'),
      })
    }

    const invalidateBoardQueries = () => {
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey.some((segment) => segment === 'boards'),
      })

      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey.some((segment) => segment === 'swimlane'),
      })

      void queryClient.invalidateQueries({
        queryKey: projectsQueryKeys.projects,
      })
    }

    const invalidateNotificationQueries = () => {
      queryClient.setQueryData<number | undefined>(
        notificationsQueryKeys.notificationsUnreadCount,
        (previousUnreadCount) =>
          typeof previousUnreadCount === 'number' ? previousUnreadCount + 1 : previousUnreadCount,
      )

      void queryClient.invalidateQueries({ queryKey: notificationsQueryKeys.notifications })
      void queryClient.invalidateQueries({ queryKey: notificationsQueryKeys.notificationsUnreadCount })
    }

    registerAliases(
      ['CardCreated', 'cardCreated'],
      invalidateCardQueriesForBoard as (...args: never[]) => void,
    )
    registerAliases(
      ['CardUpdated', 'cardUpdated'],
      invalidateCardQueriesForBoard as (...args: never[]) => void,
    )
    registerAliases(
      ['CardDeleted', 'cardDeleted'],
      invalidateCardQueriesBroad as (...args: never[]) => void,
    )
    registerAliases(
      ['CardMoved', 'cardMoved'],
      invalidateCardQueriesForBoard as (...args: never[]) => void,
    )

    registerAliases(['ColumnCreated', 'columnCreated'], invalidateColumnQueries)
    registerAliases(['ColumnUpdated', 'columnUpdated'], invalidateColumnQueries)
    registerAliases(['ColumnDeleted', 'columnDeleted'], invalidateColumnQueries)

    registerAliases(['BoardCreated', 'boardCreated'], invalidateBoardQueries)
    registerAliases(['BoardUpdated', 'boardUpdated'], invalidateBoardQueries)
    registerAliases(['BoardDeleted', 'boardDeleted'], invalidateBoardQueries)

    const invalidateCommentQueries = (comment: { cardId?: string }) => {
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey.some((segment) => segment === 'comments'),
      })

      if (comment.cardId) {
        void queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(comment.cardId) })
      } else {
        void queryClient.invalidateQueries({
          predicate: (query) => query.queryKey.some((segment) => segment === 'cards'),
        })
      }
    }

    registerAliases(
      ['CommentCreated', 'commentCreated'],
      invalidateCommentQueries as (...args: never[]) => void,
    )
    registerAliases(
      ['CommentUpdated', 'commentUpdated'],
      invalidateCommentQueries as (...args: never[]) => void,
    )
    registerAliases(
      ['CommentDeleted', 'commentDeleted'],
      invalidateCommentQueries as (...args: never[]) => void,
    )

    const invalidateTagQueries = () => {
      // Cards, the board's tag list, and filter panels all embed a denormalized
      // copy of each tag (name/color), so refresh both the board and card trees
      // when a tag changes elsewhere in the project.
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey.some((segment) => segment === 'boards'),
      })
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey.some((segment) => segment === 'cards'),
      })
    }

    registerAliases(['TagCreated', 'tagCreated'], invalidateTagQueries)
    registerAliases(['TagUpdated', 'tagUpdated'], invalidateTagQueries)
    registerAliases(['TagDeleted', 'tagDeleted'], invalidateTagQueries)

    registerAliases(['NotificationReceived', 'notificationReceived'], invalidateNotificationQueries)

    const invalidatePlannerQueries = () => {
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey.some((segment) => segment === 'swimlane'),
      })
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey[0] === 'planner',
      })
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey[0] === 'cards',
      })
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey[0] === 'boards',
      })
    }

    registerAliases(['PlannerBlockChanged', 'plannerBlockChanged'], invalidatePlannerQueries)
  }

  getConnection(): HubConnection {
    if (!this.connection) {
      throw new Error('SignalR connection is not initialized in this environment.')
    }
    return this.connection
  }

  async connect(): Promise<void> {
    const connection = this.connection
    if (!connection || isSignalRDisabledForEnvironment()) {
      useRealtimeStore.getState().setConnectionState('disconnected')
      return
    }

    if (!useAuthStore.getState().isAuthenticated) {
      useRealtimeStore.getState().setConnectionState('disconnected')
      return
    }

    if (connection.state === HubConnectionState.Connected) {
      useRealtimeStore.getState().setConnectionState('connected')
      return
    }

    if (connection.state === HubConnectionState.Connecting) {
      useRealtimeStore.getState().setConnectionState('connecting')
      return
    }

    if (this.startPromise) {
      return this.startPromise
    }

    useRealtimeStore.getState().setConnectionState('connecting')

    this.startPromise = connection
      .start()
      .then(() => {
        useRealtimeStore.getState().setConnectionState('connected')
      })
      .catch((error: unknown) => {
        useRealtimeStore.getState().setConnectionState('disconnected')
        throw error
      })
      .finally(() => {
        this.startPromise = null
      })

    return this.startPromise
  }

  async disconnect(): Promise<void> {
    const connection = this.connection
    if (!connection || isSignalRDisabledForEnvironment()) {
      useRealtimeStore.getState().setConnectionState('disconnected')
      return
    }

    if (connection.state === HubConnectionState.Disconnected) {
      useRealtimeStore.getState().setConnectionState('disconnected')
      return
    }

    if (this.stopPromise) {
      return this.stopPromise
    }

    this.stopPromise = connection.stop().finally(() => {
      this.stopPromise = null
      useRealtimeStore.getState().setConnectionState('disconnected')
    })

    return this.stopPromise
  }

  async joinProject(projectId: string): Promise<void> {
    const connection = this.connection
    if (!connection || isSignalRDisabledForEnvironment()) {
      return
    }

    await this.connect()

    if (connection.state !== HubConnectionState.Connected) {
      throw new Error('SignalR connection is not ready.')
    }

    await connection.invoke('JoinProject', projectId)
  }

  async leaveProject(projectId: string): Promise<void> {
    const connection = this.connection
    if (!connection || isSignalRDisabledForEnvironment()) {
      return
    }

    if (connection.state !== HubConnectionState.Connected) {
      return
    }

    await connection.invoke('LeaveProject', projectId)
  }
}

export const signalRService = new SignalRService()
