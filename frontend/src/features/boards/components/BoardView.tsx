import { useNavigate } from '@tanstack/react-router'
import {
  DndContext,
  DragOverlay,
  MouseSensor,
  TouchSensor,
  closestCorners,
  rectIntersection,
  useDroppable,
  useSensor,
  useSensors,
  type CollisionDetection,
  type DragEndEvent,
  type DragMoveEvent,
  type DragOverEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import {
  SortableContext,
  arrayMove,
  horizontalListSortingStrategy,
  useSortable,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import AddIcon from '@mui/icons-material/Add'
import ArchiveOutlinedIcon from '@mui/icons-material/ArchiveOutlined'
import CloseIcon from '@mui/icons-material/Close'
import DeleteForeverIcon from '@mui/icons-material/DeleteForever'
import DragIndicatorIcon from '@mui/icons-material/DragIndicator'
import LabelOutlinedIcon from '@mui/icons-material/LabelOutlined'
import MoreVertIcon from '@mui/icons-material/MoreVert'
import RestoreIcon from '@mui/icons-material/Restore'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Chip from '@mui/material/Chip'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogContentText from '@mui/material/DialogContentText'
import DialogTitle from '@mui/material/DialogTitle'
import Divider from '@mui/material/Divider'
import Drawer from '@mui/material/Drawer'
import IconButton from '@mui/material/IconButton'
import List from '@mui/material/List'
import ListItem from '@mui/material/ListItem'
import ListItemIcon from '@mui/material/ListItemIcon'
import ListItemText from '@mui/material/ListItemText'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import Portal from '@mui/material/Portal'
import Stack from '@mui/material/Stack'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import useMediaQuery from '@mui/material/useMediaQuery'
import { useTheme } from '@mui/material/styles'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  useArchiveBoard,
  useArchiveColumn,
  useArchivedColumnsByBoard,
  useBoard,
  useColumns,
  useCreateColumn,
  usePurgeColumn,
  useReorderColumns,
  useRestoreColumn,
  useUpdateBoard,
  useUpdateColumn,
} from '@/features/boards/api/boards'
import {
  useArchiveCard,
  useArchivedCardsByBoard,
  useCards,
  useCreateCard,
  useMoveCard,
  usePurgeCard,
  useRestoreCard,
} from '@/features/cards'
import { isMemberPlus, useProject, useProjectMembers } from '@/features/projects'
import { useFilterCards } from '@/features/search'
import { useBoardTags } from '@/features/cards'
import {
  applyClientCardFilters,
  hasActiveClientFilters,
  parseClientFiltersFromSearch,
} from '@/features/cards'
import { useAuthStore } from '@/features/auth'
import { useUiStore } from '@/stores/uiStore'
import type { Card as BoardCard, Column, Guid, PlannedBlock, ProjectRole } from '@/lib/types'
import { CardList } from '@/features/cards'
import { CardDetailDialog } from '@/features/cards'
import { BoardSkeleton } from '@/components/loading/BoardSkeleton'
import { SubscribeButton } from '@/features/subscriptions'
import { BoardFilterControl } from '@/features/search'
import { TagManagementDialog } from '@/features/tags'
import {
  BoardPlannerPanel,
  BOARD_PLANNER_PANEL_WIDTH,
  PLANNER_DROP_ID,
  type BoardPlannerPanelHandle,
} from '@/features/planner'
import { PlannerTimeBlock } from '@/features/planner'
import {
  yToTime as plannerYToTime,
  timeToY as plannerTimeToY,
  blockHeight as plannerBlockHeight,
  ROW_HEIGHT_PX as PLANNER_ROW_HEIGHT,
} from '@/features/planner'
import { BoardScrollbar } from './BoardScrollbar'

export type BoardSearch = {
  cardId?: string
  tagIds?: string
  userIds?: string
  due?: 'overdue' | 'today' | 'week' | 'none'
  assign?: 'me' | 'unassigned' | 'multiple'
  activity?: '24h' | '7d' | '30d' | 'stale'
  createdByIds?: string
  hasAttachments?: '1' | '0'
  hasComments?: '1' | '0'
  estMin?: number
  estMax?: number
}

interface BoardViewProps {
  projectId: string
  boardId: string
  search: BoardSearch
}

type DragData = {
  type: 'column' | 'card' | 'column-drop' | 'block'
  columnId?: string
  cardId?: string
  block?: PlannedBlock
}

function calculatePosition(cards: BoardCard[], targetIndex: number): number {
  if (cards.length === 0) return 1000
  if (targetIndex === 0) return cards[0].position - 1000
  if (targetIndex >= cards.length) return cards[cards.length - 1].position + 1000
  return Math.floor((cards[targetIndex - 1].position + cards[targetIndex].position) / 2)
}

function cloneCardsByColumn(source: Map<string, BoardCard[]>): Map<string, BoardCard[]> {
  return new Map(
    Array.from(source.entries(), ([columnId, columnCards]) => [columnId, [...columnCards]]),
  )
}

/**
 * Custom collision detection: for column drags, only compare horizontal centers
 * so that tall columns don't block shorter columns from being reordered.
 * Card drags check the planner droppable first (via rect intersection),
 * then fall back to closestCorners for column/card targets.
 */
const kanbanCollisionDetection: CollisionDetection = (args) => {
  const activeData = args.active.data.current as DragData | undefined

  if (activeData?.type === 'column') {
    const columnContainers = args.droppableContainers.filter((container) => {
      const data = container.data.current as DragData | undefined
      return data?.type === 'column'
    })

    if (columnContainers.length === 0) return []

    const activeCenterX = args.collisionRect.left + args.collisionRect.width / 2
    let closestId: string | number | null = null
    let closestDist = Infinity

    for (const container of columnContainers) {
      const rect = container.rect.current
      if (!rect) continue
      const dist = Math.abs(rect.left + rect.width / 2 - activeCenterX)
      if (dist < closestDist) {
        closestDist = dist
        closestId = container.id
      }
    }

    return closestId != null ? [{ id: closestId }] : []
  }

  // For card drags: check if dragging over the planner droppable first
  if (activeData?.type === 'card') {
    const plannerContainers = args.droppableContainers.filter(
      (container) => container.id === PLANNER_DROP_ID,
    )
    if (plannerContainers.length > 0) {
      const plannerCollisions = rectIntersection({
        ...args,
        droppableContainers: plannerContainers,
      })
      if (plannerCollisions.length > 0) {
        return plannerCollisions
      }
    }
  }

  return closestCorners(args)
}

export function BoardView({ projectId, boardId, search }: BoardViewProps) {
  const navigate = useNavigate()
  const theme = useTheme()
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'))

  const currentUserId = useAuthStore((state) => state.user?.id)

  const projectQuery = useProject(projectId)
  const membersQuery = useProjectMembers(projectId)
  const tagsQuery = useBoardTags(boardId)
  const boardQuery = useBoard(boardId)
  const columnsQuery = useColumns(boardId)
  const cardsQuery = useCards(boardId, 1, 500)

  const updateBoardMutation = useUpdateBoard()
  const archiveBoardMutation = useArchiveBoard()
  const createColumnMutation = useCreateColumn()
  const updateColumnMutation = useUpdateColumn()
  const reorderColumnsMutation = useReorderColumns()
  const createCardMutation = useCreateCard()
  const moveCardMutation = useMoveCard()
  const archiveCardMutation = useArchiveCard()
  const archiveColumnMutation = useArchiveColumn()

  const board = boardQuery.data
  const columns = useMemo(() => columnsQuery.data ?? [], [columnsQuery.data])
  const cards = useMemo(() => cardsQuery.data?.items ?? [], [cardsQuery.data?.items])
  const members = useMemo(() => membersQuery.data ?? [], [membersQuery.data])
  const project = projectQuery.data
  const parsedFilters = useMemo(
    () => ({
      tagIds: parseCsvGuidList(search.tagIds),
      userIds: parseCsvGuidList(search.userIds),
    }),
    [search.tagIds, search.userIds],
  )
  const clientFilterSearch = useMemo(
    () => ({
      due: search.due,
      assign: search.assign,
      activity: search.activity,
      createdByIds: search.createdByIds,
      hasAttachments: search.hasAttachments,
      hasComments: search.hasComments,
      estMin: search.estMin,
      estMax: search.estMax,
    }),
    [
      search.due,
      search.assign,
      search.activity,
      search.createdByIds,
      search.hasAttachments,
      search.hasComments,
      search.estMin,
      search.estMax,
    ],
  )
  const clientFilters = useMemo(
    () => parseClientFiltersFromSearch(clientFilterSearch),
    [clientFilterSearch],
  )
  const hasActiveClientSide = hasActiveClientFilters(clientFilters)
  const filterSearchParams = useMemo(
    () => ({
      tagIds: search.tagIds,
      userIds: search.userIds,
      due: search.due,
      assign: search.assign,
      activity: search.activity,
      createdByIds: search.createdByIds,
      hasAttachments: search.hasAttachments,
      hasComments: search.hasComments,
      estMin: search.estMin,
      estMax: search.estMax,
    }),
    [
      search.tagIds,
      search.userIds,
      search.due,
      search.assign,
      search.activity,
      search.createdByIds,
      search.hasAttachments,
      search.hasComments,
      search.estMin,
      search.estMax,
    ],
  )
  const hasActiveBackendFilters =
    parsedFilters.tagIds.length > 0 || parsedFilters.userIds.length > 0
  const filteredCardsQuery = useFilterCards(boardId, parsedFilters)

  // Show the board skeleton until BOTH columns and cards have resolved, so the
  // real board appears fully populated instead of flashing empty columns while
  // cards are still loading.
  const isBoardLoading =
    columnsQuery.isLoading ||
    cardsQuery.isLoading ||
    (hasActiveBackendFilters && filteredCardsQuery.isLoading)

  const visibleCards = useMemo(() => {
    const base = hasActiveBackendFilters ? (filteredCardsQuery.data ?? []) : cards
    if (!hasActiveClientSide) {
      return base
    }
    return applyClientCardFilters(base, clientFilters, { currentUserId })
  }, [
    cards,
    clientFilters,
    currentUserId,
    filteredCardsQuery.data,
    hasActiveBackendFilters,
    hasActiveClientSide,
  ])

  const cardsByColumn = useMemo(() => {
    const grouped = new Map<string, BoardCard[]>()

    visibleCards.forEach((card) => {
      const current = grouped.get(card.columnId)
      if (current) {
        current.push(card)
      } else {
        grouped.set(card.columnId, [card])
      }
    })

    grouped.forEach((columnCards) => {
      columnCards.sort((first, second) => first.position - second.position)
    })

    return grouped
  }, [visibleCards])

  const [optimisticCardsByColumn, setOptimisticCardsByColumn] = useState<Map<
    string,
    BoardCard[]
  > | null>(null)
  const displayCardsByColumn = optimisticCardsByColumn ?? cardsByColumn

  const currentUserRole = useMemo(() => {
    if (!currentUserId) {
      return undefined
    }

    if (project?.ownerId === currentUserId) {
      return 0 as ProjectRole
    }

    return members.find((member) => member.userId === currentUserId)?.role
  }, [currentUserId, members, project?.ownerId])

  const canManageBoards = currentUserRole !== undefined && currentUserRole <= 2
  const canManageTags = currentUserRole !== undefined && currentUserRole <= 2
  const canUsePlanner = isMemberPlus(currentUserRole)

  const archiveDrawerOpen = useUiStore((state) => state.boardArchiveDrawerOpen)
  const setArchiveDrawerOpen = useUiStore((state) => state.setBoardArchiveDrawerOpen)
  const archivedColumnsQuery = useArchivedColumnsByBoard(boardId, archiveDrawerOpen)
  const archivedCardsQuery = useArchivedCardsByBoard(boardId, archiveDrawerOpen)
  const restoreColumnMutation = useRestoreColumn()
  const restoreCardMutation = useRestoreCard()
  const purgeColumnMutation = usePurgeColumn()
  const purgeCardMutation = usePurgeCard()
  const [archiveDrawerTab, setArchiveDrawerTab] = useState<'columns' | 'cards'>('columns')

  // A card whose list is also archived cannot be restored on its own — the list
  // must be restored first. Cross-reference against the archived columns already
  // loaded for the "Lists" tab so this does not depend on card.column.deletedAt.
  const archivedColumnIds = useMemo(
    () => new Set((archivedColumnsQuery.data ?? []).map((column) => column.id)),
    [archivedColumnsQuery.data],
  )

  const plannerOpen = useUiStore((state) => state.boardPlannerOpen)
  const setPlannerOpen = useUiStore((state) => state.setBoardPlannerOpen)
  const plannerRef = useRef<BoardPlannerPanelHandle>(null)
  const [plannerWidth, setPlannerWidth] = useState(BOARD_PLANNER_PANEL_WIDTH)
  const isResizingPlanner = useRef(false)
  const [tagDialogOpen, setTagDialogOpen] = useState(false)
  const [selectedCardIds, setSelectedCardIds] = useState<Set<Guid>>(new Set())
  const [selectionMode, setSelectionMode] = useState(false)
  const [isBulkArchiving, setIsBulkArchiving] = useState(false)
  const [archiveDialogOpen, setArchiveDialogOpen] = useState(false)
  const [archiveCardTarget, setArchiveCardTarget] = useState<BoardCard | null>(null)
  const [createColumnName, setCreateColumnName] = useState('')
  const [isAddingColumn, setIsAddingColumn] = useState(false)
  const [editBoardName, setEditBoardName] = useState('')
  const [isEditingBoardName, setIsEditingBoardName] = useState(false)
  const [editColumnNames, setEditColumnNames] = useState<Record<string, string>>({})
  const [orderedColumns, setOrderedColumns] = useState<Column[]>([])
  const [createCardDraftByColumn, setCreateCardDraftByColumn] = useState<Record<string, string>>({})
  const selectedCardId = search.cardId ?? null

  const [createCardTagIdsByColumn, setCreateCardTagIdsByColumn] = useState<Record<string, Guid[]>>(
    {},
  )
  const [createCardErrorByColumn, setCreateCardErrorByColumn] = useState<
    Record<string, string | null>
  >({})
  const [activeDragCard, setActiveDragCard] = useState<BoardCard | null>(null)
  const [activeDragColumn, setActiveDragColumn] = useState<Column | null>(null)
  const [activeDragBlock, setActiveDragBlock] = useState<PlannedBlock | null>(null)
  const isDragActive = activeDragCard !== null || activeDragColumn !== null || activeDragBlock !== null

  // Trello-style discrete column jump for the horizontal columns rail on mobile.
  // dnd-kit's built-in autoScroll fights iOS momentum + scroll-snap and feels
  // choppy. Instead, when the drag pointer sits inside the edge zone we
  // smooth-scroll one column over, then wait through a cooldown before the
  // next jump, matching how Trello's native app advances the board.
  const lastEdgeJumpAtRef = useRef(0)
  const wasInEdgeZoneRef = useRef(false)
  // ~300ms of the cooldown is consumed by the smooth-scroll animation itself,
  // so the perceived pause between consecutive jumps is roughly half this.
  const EDGE_JUMP_COOLDOWN_MS = 900

  const stopEdgePan = useCallback(() => {
    lastEdgeJumpAtRef.current = 0
    wasInEdgeZoneRef.current = false
  }, [])

  // Defense-in-depth: lock text selection on the whole document for the
  // duration of a drag. The Card already sets user-select: none, but stray
  // selection started during the 200ms TouchSensor activation delay (or on
  // sibling elements like column headers) can leak through and trigger iOS
  // selection handles mid-drag, which is what causes the jitter.
  useEffect(() => {
    if (!isDragActive) return
    const { body } = document
    const prevUserSelect = body.style.userSelect
    const prevWebkitUserSelect = body.style.webkitUserSelect
    const prevTouchCallout = body.style.getPropertyValue('-webkit-touch-callout')
    body.style.userSelect = 'none'
    body.style.webkitUserSelect = 'none'
    body.style.setProperty('-webkit-touch-callout', 'none')
    return () => {
      body.style.userSelect = prevUserSelect
      body.style.webkitUserSelect = prevWebkitUserSelect
      body.style.setProperty('-webkit-touch-callout', prevTouchCallout)
    }
  }, [isDragActive])
  const [plannerDropPreview, setPlannerDropPreview] = useState<{
    top: number
    height: number
  } | null>(null)

  const columnsScrollRef = useRef<HTMLDivElement | null>(null)
  const [activeColumnIndex, setActiveColumnIndex] = useState(0)
  const [overflowMenuAnchor, setOverflowMenuAnchor] = useState<HTMLElement | null>(null)

  const sensors = useSensors(
    useSensor(MouseSensor, {
      activationConstraint: {
        distance: 8,
      },
    }),
    useSensor(TouchSensor, {
      activationConstraint: {
        delay: 200,
        tolerance: 5,
      },
    }),
  )

  const getRailItems = useCallback((node: HTMLElement) => {
    return node.querySelectorAll<HTMLElement>('[data-board-rail-item], [data-column-id]')
  }, [])

  const handleColumnsScroll = useCallback(() => {
    if (!isMobile) return
    const node = columnsScrollRef.current
    if (!node) return
    const children = getRailItems(node)
    if (children.length === 0) return
    const scrollLeft = node.scrollLeft
    let closestIdx = 0
    let closestDist = Infinity
    children.forEach((child, idx) => {
      const dist = Math.abs(child.offsetLeft - scrollLeft)
      if (dist < closestDist) {
        closestDist = dist
        closestIdx = idx
      }
    })
    setActiveColumnIndex(closestIdx)
  }, [getRailItems, isMobile])

  const scrollToRailItem = useCallback(
    (index: number) => {
      const node = columnsScrollRef.current
      if (!node) return
      const children = getRailItems(node)
      const target = children.item(index)
      if (target) {
        node.scrollTo({ left: target.offsetLeft, behavior: 'smooth' })
      }
    },
    [getRailItems],
  )

  useEffect(() => {
    setEditBoardName(board?.name ?? '')
  }, [board?.name])

  useEffect(() => {
    setOrderedColumns(columns)
  }, [columns])

  useEffect(() => {
    setEditColumnNames((previous) => {
      const next: Record<string, string> = {}

      columns.forEach((column) => {
        next[column.id] = previous[column.id] ?? column.name
      })

      return next
    })
  }, [columns])

  useEffect(() => {
    if (!selectedCardId) {
      return
    }

    if (cardsQuery.isLoading) {
      return
    }

    const exists = cards.some((card) => card.id === selectedCardId)
    if (!exists) {
      void navigate({
        to: '/projects/$projectId/boards/$boardId',
        params: {
          projectId,
          boardId,
        },
        search: {
          ...filterSearchParams,
        },
      })
    }
  }, [
    boardId,
    cards,
    cardsQuery.isLoading,
    filterSearchParams,
    navigate,
    projectId,
    selectedCardId,
  ])

  const trimmedCreateColumnName = createColumnName.trim()
  const trimmedEditName = editBoardName.trim()

  const canCreateColumn =
    canManageBoards &&
    Boolean(board) &&
    trimmedCreateColumnName.length > 0 &&
    !createColumnMutation.isPending
  const hasBoardNameChanged = Boolean(board) && trimmedEditName !== board?.name
  const canSaveBoardName =
    canManageBoards &&
    hasBoardNameChanged &&
    trimmedEditName.length > 0 &&
    !updateBoardMutation.isPending

  const handleSaveBoardName = async () => {
    if (!board || !canSaveBoardName) {
      return
    }

    await updateBoardMutation.mutateAsync({
      id: board.id,
      data: {
        name: trimmedEditName,
        position: board.position,
      },
    })
  }

  const handleCreateColumn = async () => {
    if (!board || !canCreateColumn) {
      return
    }

    await createColumnMutation.mutateAsync({
      boardId: board.id,
      data: {
        name: trimmedCreateColumnName,
      },
    })

    setCreateColumnName('')
  }

  const handleSaveColumnName = async (column: Column) => {
    const draftName = (editColumnNames[column.id] ?? '').trim()
    if (
      !canManageBoards ||
      updateColumnMutation.isPending ||
      draftName.length === 0 ||
      draftName === column.name
    ) {
      return
    }

    await updateColumnMutation.mutateAsync({
      id: column.id,
      data: {
        name: draftName,
      },
    })
  }

  const clearCreateCardDraft = (columnId: string) => {
    setCreateCardDraftByColumn((previous) => {
      if (!(columnId in previous)) {
        return previous
      }

      const next = { ...previous }
      delete next[columnId]
      return next
    })

    setCreateCardErrorByColumn((previous) => {
      if (!(columnId in previous)) {
        return previous
      }

      const next = { ...previous }
      delete next[columnId]
      return next
    })

    setCreateCardTagIdsByColumn((previous) => {
      if (!(columnId in previous)) {
        return previous
      }

      const next = { ...previous }
      delete next[columnId]
      return next
    })
  }

  const handleCreateCardDraftChange = (columnId: string, value: string) => {
    setCreateCardDraftByColumn((previous) => ({
      ...previous,
      [columnId]: value,
    }))

    setCreateCardErrorByColumn((previous) => ({
      ...previous,
      [columnId]: null,
    }))
  }

  const handleSubmitInlineCard = async (columnId: string, options?: { fromBlur?: boolean }) => {
    if (!canManageBoards || createCardMutation.isPending) {
      return
    }

    const rawTitle = createCardDraftByColumn[columnId] ?? ''
    const title = rawTitle.trim()

    if (title.length === 0) {
      if (options?.fromBlur) {
        clearCreateCardDraft(columnId)
      }

      return
    }

    try {
      const selectedTagIds = createCardTagIdsByColumn[columnId] ?? []

      await createCardMutation.mutateAsync({
        columnId,
        data: {
          title,
          tagIds: selectedTagIds.length > 0 ? selectedTagIds : undefined,
        },
      })

      clearCreateCardDraft(columnId)
    } catch {
      setCreateCardErrorByColumn((previous) => ({
        ...previous,
        [columnId]: 'Unable to create task. Please try again.',
      }))
    }
  }

  const handleDragStart = (event: DragStartEvent) => {
    // If the user had text selected when the drag activated, iOS keeps the
    // selection handles and "Copy/Look Up…" callout alive: those handles
    // capture touch events and make the drag jitter. Clear the selection and
    // blur any focused editable so dnd-kit owns the gesture cleanly.
    const selection = window.getSelection()
    if (selection && !selection.isCollapsed) {
      selection.removeAllRanges()
    }
    const activeElement = document.activeElement
    if (activeElement instanceof HTMLElement) {
      const tag = activeElement.tagName
      if (tag === 'INPUT' || tag === 'TEXTAREA' || activeElement.isContentEditable) {
        activeElement.blur()
      }
    }

    const activeData = event.active.data?.current as DragData | undefined

    if (activeData?.type === 'block') {
      setActiveDragBlock(activeData.block as PlannedBlock)
      setActiveDragCard(null)
      setActiveDragColumn(null)
      return
    }

    if (activeData?.type === 'card' && activeData.cardId) {
      const draggedCard = cards.find((card) => card.id === activeData.cardId)
      setActiveDragCard(draggedCard ?? null)
      setActiveDragColumn(null)
      setActiveDragBlock(null)
      return
    }

    const columnId = activeData?.columnId ?? String(event.active.id)
    const draggedColumn = orderedColumns.find((col) => col.id === columnId)
    setActiveDragColumn(draggedColumn ?? null)
    setActiveDragCard(null)
    setActiveDragBlock(null)
  }

  const handleDragCancel = () => {
    stopEdgePan()
    setActiveDragCard(null)
    setActiveDragColumn(null)
    setActiveDragBlock(null)
    setOptimisticCardsByColumn(null)
    setPlannerDropPreview(null)
  }

  const handlePlannerResizeStart = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault()
      isResizingPlanner.current = true
      const startX = e.clientX
      const startWidth = plannerWidth

      const onMouseMove = (ev: MouseEvent) => {
        if (!isResizingPlanner.current) return
        const newWidth = Math.max(280, Math.min(600, startWidth + (ev.clientX - startX)))
        setPlannerWidth(newWidth)
      }

      const onMouseUp = () => {
        isResizingPlanner.current = false
        document.removeEventListener('mousemove', onMouseMove)
        document.removeEventListener('mouseup', onMouseUp)
        document.body.style.cursor = ''
        document.body.style.userSelect = ''
      }

      document.body.style.cursor = 'col-resize'
      document.body.style.userSelect = 'none'
      document.addEventListener('mousemove', onMouseMove)
      document.addEventListener('mouseup', onMouseUp)
    },
    [plannerWidth],
  )

  const handleDragMove = (event: DragMoveEvent) => {
    const { active, over } = event

    // Edge jump: when dragging a card/column on mobile and the pointer enters
    // the left/right edge zone, smooth-scroll one column over, then enforce a
    // cooldown before the next jump. If the user leaves the edge zone the
    // cooldown resets so the next jump is immediate (Trello-style).
    if (isMobile && (activeDragCard !== null || activeDragColumn !== null)) {
      const container = columnsScrollRef.current
      const dragRect = active.rect.current.translated
      if (container && dragRect) {
        const containerRect = container.getBoundingClientRect()
        const dragCenterX = (dragRect.left + dragRect.right) / 2
        const edgeZone = Math.max(64, containerRect.width * 0.22)
        const leftEdge = containerRect.left + edgeZone
        const rightEdge = containerRect.right - edgeZone

        const canScrollLeft = container.scrollLeft > 1
        const canScrollRight =
          container.scrollLeft + containerRect.width < container.scrollWidth - 1
        const inLeftZone = dragCenterX < leftEdge && canScrollLeft
        const inRightZone = dragCenterX > rightEdge && canScrollRight

        if (!inLeftZone && !inRightZone) {
          // Leaving the edge zone clears the cooldown so re-entering jumps
          // immediately instead of waiting out a stale timer.
          wasInEdgeZoneRef.current = false
          lastEdgeJumpAtRef.current = 0
        } else {
          const now = performance.now()
          const sinceLastJump = now - lastEdgeJumpAtRef.current
          const justEnteredZone = !wasInEdgeZoneRef.current
          wasInEdgeZoneRef.current = true

          if (justEnteredZone || sinceLastJump >= EDGE_JUMP_COOLDOWN_MS) {
            const railItems = Array.from(getRailItems(container))
            if (railItems.length > 0) {
              const currentScroll = container.scrollLeft
              // Pick the rail item closest to the current scroll position.
              let currentIdx = 0
              let bestDist = Infinity
              railItems.forEach((item, idx) => {
                const dist = Math.abs(item.offsetLeft - currentScroll)
                if (dist < bestDist) {
                  bestDist = dist
                  currentIdx = idx
                }
              })
              const direction = inLeftZone ? -1 : 1
              const targetIdx = Math.max(
                0,
                Math.min(railItems.length - 1, currentIdx + direction),
              )
              const targetItem = railItems[targetIdx]
              if (targetItem && targetItem.offsetLeft !== currentScroll) {
                container.scrollTo({
                  left: targetItem.offsetLeft,
                  behavior: 'smooth',
                })
                lastEdgeJumpAtRef.current = now
              }
            }
          }
        }
      }
    }

    if (!over || over.id !== PLANNER_DROP_ID) {
      setPlannerDropPreview(null)
      return
    }

    const data = active.data.current as DragData | undefined
    if (!data) {
      setPlannerDropPreview(null)
      return
    }

    const dropAreaRect = plannerRef.current?.getDropAreaRect()
    if (!dropAreaRect) {
      setPlannerDropPreview(null)
      return
    }

    const translatedTop = active.rect.current.translated?.top ?? 0
    const relativeY = translatedTop - dropAreaRect.top
    const snappedTime = plannerYToTime(Math.max(0, relativeY))
    const snappedTop = plannerTimeToY(snappedTime)

    let previewHeight: number
    if (data.type === 'block') {
      const block = (data as { type: string; block: PlannedBlock }).block
      previewHeight = plannerBlockHeight(block.startTime, block.endTime)
    } else {
      previewHeight = PLANNER_ROW_HEIGHT * 4 // 1 hour default
    }

    setPlannerDropPreview({ top: snappedTop, height: Math.max(PLANNER_ROW_HEIGHT, previewHeight) })
  }

  const handleDragOver = (event: DragOverEvent) => {
    const { active, over } = event
    if (!over || !activeDragCard) return

    // Skip column-based logic when hovering over the planner
    if (over.id === PLANNER_DROP_ID) return

    const activeData = active.data.current as DragData | undefined
    if (activeData?.type !== 'card') return

    const overData = over.data.current as DragData | undefined
    if (!overData) return

    let overColumnId: string | undefined
    if (overData.type === 'card') {
      overColumnId = overData.columnId
    } else if (overData.type === 'column-drop' || overData.type === 'column') {
      overColumnId = overData.columnId
    }

    if (!overColumnId) return

    const currentMap = optimisticCardsByColumn ?? cardsByColumn
    let activeColumnId: string | undefined
    for (const [colId, colCards] of currentMap.entries()) {
      if (colCards.some((c) => c.id === activeDragCard.id)) {
        activeColumnId = colId
        break
      }
    }

    if (!activeColumnId || activeColumnId === overColumnId) return

    const newMap = cloneCardsByColumn(currentMap)
    const sourceCards = newMap.get(activeColumnId) ?? []
    const destCards = newMap.get(overColumnId) ?? []

    const cardIdx = sourceCards.findIndex((c) => c.id === activeDragCard.id)
    if (cardIdx < 0) return

    const [card] = sourceCards.splice(cardIdx, 1)
    newMap.set(activeColumnId, sourceCards)

    let insertIdx = destCards.length
    if (overData.type === 'card' && overData.cardId) {
      const overIdx = destCards.findIndex((c) => c.id === overData.cardId)
      if (overIdx >= 0) insertIdx = overIdx
    }

    destCards.splice(insertIdx, 0, { ...card, columnId: overColumnId })
    newMap.set(overColumnId, destCards)

    setOptimisticCardsByColumn(newMap)
  }

  const handleDragEnd = async (event: DragEndEvent) => {
    const activeData = event.active.data?.current as DragData | undefined
    const overData = event.over?.data?.current as DragData | undefined

    const resetDragState = () => {
      stopEdgePan()
      setActiveDragCard(null)
      setActiveDragColumn(null)
      setActiveDragBlock(null)
      setOptimisticCardsByColumn(null)
      setPlannerDropPreview(null)
    }

    // Handle card drop onto planner timeline
    if (activeDragCard && event.over?.id === PLANNER_DROP_ID && plannerRef.current) {
      const translatedTop = event.active.rect.current.translated?.top ?? 0
      plannerRef.current.handleCardDrop(activeDragCard.id, activeDragCard.title, translatedTop)
      resetDragState()
      return
    }

    // Handle planner block move (drag within timeline)
    if (activeData?.type === 'block' && plannerRef.current) {
      const block = activeData.block as PlannedBlock
      const translatedTop = event.active.rect.current.translated?.top ?? 0
      plannerRef.current.handleBlockMove(block.id, translatedTop)
      resetDragState()
      return
    }

    const reorderColumnsFromEvent = async () => {
      if (!canManageBoards || reorderColumnsMutation.isPending) {
        resetDragState()
        return
      }

      const { active, over } = event
      if (!over || active.id === over.id) {
        resetDragState()
        return
      }

      const previousColumns = orderedColumns
      const oldIndex = previousColumns.findIndex((column) => column.id === active.id)
      const newIndex = previousColumns.findIndex((column) => column.id === over.id)

      if (oldIndex < 0 || newIndex < 0) {
        resetDragState()
        return
      }

      const reorderedColumns = arrayMove(previousColumns, oldIndex, newIndex)
      const movedColumn = reorderedColumns[newIndex]
      const beforeColumn = newIndex > 0 ? reorderedColumns[newIndex - 1] : null
      const afterColumn =
        newIndex < reorderedColumns.length - 1 ? reorderedColumns[newIndex + 1] : null

      setOrderedColumns(reorderedColumns)

      try {
        await reorderColumnsMutation.mutateAsync({
          id: movedColumn.id,
          data: {
            beforeColumnId: beforeColumn?.id ?? null,
            afterColumnId: afterColumn?.id ?? null,
          },
        })
      } catch {
        setOrderedColumns(previousColumns)
      } finally {
        resetDragState()
      }
    }

    if (!activeData) {
      await reorderColumnsFromEvent()
      return
    }

    if (activeData.type === 'column') {
      await reorderColumnsFromEvent()
      return
    }

    if (activeData.type !== 'card' || !activeDragCard) {
      resetDragState()
      return
    }

    // Determine target column: prefer optimistic state (set by onDragOver for cross-column),
    // then over data
    let targetColumnId: string | undefined

    if (optimisticCardsByColumn) {
      for (const [colId, colCards] of optimisticCardsByColumn.entries()) {
        if (colCards.some((c) => c.id === activeDragCard.id)) {
          targetColumnId = colId
          break
        }
      }
    }

    if (!targetColumnId && overData?.columnId) {
      targetColumnId = overData.columnId
    }

    if (!targetColumnId) {
      resetDragState()
      return
    }

    // Same-column reorder (no cross-column during this drag)
    const isSameColumn = targetColumnId === activeDragCard.columnId && !optimisticCardsByColumn

    if (isSameColumn) {
      if (!overData?.cardId || overData.cardId === activeDragCard.id || overData.type !== 'card') {
        resetDragState()
        return
      }

      const columnCards = cardsByColumn.get(targetColumnId) ?? []
      const oldIndex = columnCards.findIndex((c) => c.id === activeDragCard.id)
      const newIndex = columnCards.findIndex((c) => c.id === overData.cardId)

      if (oldIndex < 0 || newIndex < 0 || oldIndex === newIndex) {
        resetDragState()
        return
      }

      const reordered = arrayMove(columnCards, oldIndex, newIndex)
      const movedIdx = reordered.findIndex((c) => c.id === activeDragCard.id)
      const cardsWithout = reordered.filter((c) => c.id !== activeDragCard.id)
      const nextPosition = calculatePosition(cardsWithout, movedIdx)
      const movedCard: BoardCard = { ...activeDragCard, position: nextPosition }

      const optimisticMap = cloneCardsByColumn(cardsByColumn)
      optimisticMap.set(
        targetColumnId,
        reordered.map((c) => (c.id === activeDragCard.id ? movedCard : c)),
      )
      setOptimisticCardsByColumn(optimisticMap)

      try {
        await moveCardMutation.mutateAsync({
          id: movedCard.id,
          boardId,
          data: {
            columnId: targetColumnId,
            position: movedIdx,
          },
          optimisticPosition: nextPosition,
        })
      } catch {
        // Query-layer optimistic rollback is handled by useMoveCard.
      } finally {
        resetDragState()
      }
      return
    }

    // Cross-column move
    const destinationCards = (cardsByColumn.get(targetColumnId) ?? []).filter(
      (card) => card.id !== activeDragCard.id,
    )

    let targetIndex = destinationCards.length

    if (overData?.type === 'card' && overData.cardId && overData.cardId !== activeDragCard.id) {
      const cardIndex = destinationCards.findIndex((card) => card.id === overData.cardId)
      if (cardIndex >= 0) {
        targetIndex = cardIndex
      }
    } else if (optimisticCardsByColumn) {
      const optimisticCards = optimisticCardsByColumn.get(targetColumnId) ?? []
      const optIdx = optimisticCards.findIndex((c) => c.id === activeDragCard.id)
      if (optIdx >= 0) {
        targetIndex = optIdx
      }
    }

    const nextPosition = calculatePosition(destinationCards, targetIndex)
    const movedCard: BoardCard = {
      ...activeDragCard,
      columnId: targetColumnId,
      position: nextPosition,
    }

    const optimisticMap = cloneCardsByColumn(cardsByColumn)
    const sourceColumnId = activeDragCard.columnId
    const nextSourceCards = (optimisticMap.get(sourceColumnId) ?? []).filter(
      (card) => card.id !== activeDragCard.id,
    )
    optimisticMap.set(sourceColumnId, nextSourceCards)

    const nextDestinationCards = [...destinationCards]
    nextDestinationCards.splice(targetIndex, 0, movedCard)
    optimisticMap.set(targetColumnId, nextDestinationCards)
    setOptimisticCardsByColumn(optimisticMap)

    try {
      await moveCardMutation.mutateAsync({
        id: movedCard.id,
        boardId,
        data: {
          columnId: targetColumnId,
          position: targetIndex,
        },
        optimisticPosition: nextPosition,
      })
    } catch {
      // Query-layer optimistic rollback is handled by useMoveCard.
    } finally {
      resetDragState()
    }
  }

  const handleArchiveBoard = async () => {
    if (!board || !canManageBoards || archiveBoardMutation.isPending) {
      return
    }

    await archiveBoardMutation.mutateAsync(board.id)
    setArchiveDialogOpen(false)

    navigate({
      to: '/projects/$projectId',
      params: {
        projectId,
      },
    })
  }

  const handleToggleSelectCard = useCallback((cardId: Guid) => {
    setSelectedCardIds((previous) => {
      const next = new Set(previous)
      if (next.has(cardId)) {
        next.delete(cardId)
      } else {
        next.add(cardId)
      }
      return next
    })
  }, [])

  const handleBulkArchive = async () => {
    if (selectedCardIds.size === 0 || !canManageBoards || isBulkArchiving) return
    setIsBulkArchiving(true)
    try {
      for (const cardId of selectedCardIds) {
        await archiveCardMutation.mutateAsync(cardId)
      }
      setSelectedCardIds(new Set())
      setSelectionMode(false)
    } finally {
      setIsBulkArchiving(false)
    }
  }

  const handleArchiveCard = async () => {
    if (!archiveCardTarget || !canManageBoards || archiveCardMutation.isPending) {
      return
    }

    await archiveCardMutation.mutateAsync(archiveCardTarget.id)
    setArchiveCardTarget(null)
  }

  return (
    <Box
      sx={{
        px: { xs: 1.5, sm: 2, md: 3 },
        pb: { xs: 'calc(env(safe-area-inset-bottom, 0px) + 36px)', sm: 2, md: 3 },
        pt: { xs: 1.5, sm: 2, md: 3 },
      }}
    >
      <Stack spacing={{ xs: 1, sm: 2 }}>
        {projectQuery.isLoading || membersQuery.isLoading ? (
          <Typography color="text.secondary">Loading board permissions...</Typography>
        ) : null}

        {projectQuery.isError || membersQuery.isError ? (
          <Alert severity="error">Unable to load project access details.</Alert>
        ) : null}

        {!projectQuery.isLoading && !membersQuery.isLoading && !canManageBoards ? (
          <Alert severity="info">You have read-only access to this board.</Alert>
        ) : null}

        {boardQuery.isLoading ? (
          <Typography color="text.secondary">Loading board...</Typography>
        ) : null}

        {boardQuery.isError ? <Alert severity="error">Unable to load board details.</Alert> : null}

        {!boardQuery.isLoading && !board && !boardQuery.isError ? (
          <Alert severity="warning">Board not found.</Alert>
        ) : null}

        {board ? (
          <Stack spacing={{ xs: 1, sm: 2 }}>
            {/* Compact board header */}
            <Stack spacing={{ xs: 0.75, sm: 1.5 }}>
              <Stack
                direction="row"
                spacing={{ xs: 0.5, sm: 2 }}
                alignItems="center"
                justifyContent="space-between"
              >
                {/* Left: board name + card count */}
                <Stack
                  direction="row"
                  spacing={1.5}
                  alignItems="center"
                  sx={{ minWidth: 0, flex: 1 }}
                >
                  {isEditingBoardName && canManageBoards ? (
                    <TextField
                      value={editBoardName}
                      onChange={(event) => setEditBoardName(event.target.value)}
                      onBlur={() => {
                        if (hasBoardNameChanged && canSaveBoardName) {
                          void handleSaveBoardName()
                        }
                        setIsEditingBoardName(false)
                      }}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter') {
                          event.preventDefault()
                          if (hasBoardNameChanged && canSaveBoardName) {
                            void handleSaveBoardName()
                          }
                          setIsEditingBoardName(false)
                        }
                        if (event.key === 'Escape') {
                          event.preventDefault()
                          setEditBoardName(board.name)
                          setIsEditingBoardName(false)
                        }
                      }}
                      autoFocus
                      variant="standard"
                      disabled={updateBoardMutation.isPending}
                      sx={{
                        flex: 1,
                        '& .MuiInput-underline:before': { borderBottom: 'none' },
                        '& .MuiInput-underline:hover:before': {
                          borderBottom: '2px solid',
                          borderColor: 'divider',
                        },
                        '& .MuiInput-underline:after': { borderBottom: 'none' },
                        '& .MuiInputBase-input': {
                          fontSize: '1.25rem',
                          fontWeight: 700,
                          letterSpacing: '-0.01em',
                          py: 0.25,
                        },
                      }}
                    />
                  ) : (
                    <Typography
                      variant="h3"
                      onClick={() => {
                        if (canManageBoards) {
                          setIsEditingBoardName(true)
                        }
                      }}
                      noWrap
                      sx={{
                        fontSize: '1.25rem',
                        fontWeight: 700,
                        letterSpacing: '-0.01em',
                        cursor: canManageBoards ? 'text' : 'default',
                        minWidth: 0,
                      }}
                    >
                      {board.name}
                    </Typography>
                  )}
                </Stack>

                {/* Right: toolbar */}
                <Stack direction="row" spacing={0.5} alignItems="center" sx={{ flexShrink: 0 }}>
                  <BoardFilterControl
                    projectId={projectId}
                    boardId={boardId}
                    cardId={search.cardId}
                    tagIds={search.tagIds}
                    userIds={search.userIds}
                    clientFilterSearch={clientFilterSearch}
                  />

                  {/* Desktop: show all icons inline */}
                  <Box
                    sx={{
                      display: { xs: 'none', sm: 'flex' },
                      alignItems: 'center',
                      gap: 0.5,
                    }}
                  >
                    {canManageTags ? (
                      <Tooltip title="Manage Tags">
                        <IconButton
                          size="small"
                          onClick={() => setTagDialogOpen(true)}
                          aria-label="Manage tags"
                        >
                          <LabelOutlinedIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    ) : null}

                    <SubscribeButton
                      entityType={3}
                      entityId={board.id}
                      iconButtonProps={{ size: 'small' }}
                    />

                    {canManageBoards ? (
                      <Tooltip title={selectionMode ? 'Exit multi-archive' : 'Multi-archive tasks'}>
                        <IconButton
                          size="small"
                          onClick={() => {
                            if (selectionMode) {
                              setSelectedCardIds(new Set())
                            }
                            setSelectionMode((prev) => !prev)
                          }}
                          color={selectionMode ? 'primary' : 'default'}
                          aria-label={
                            selectionMode ? 'Exit multi-archive mode' : 'Enter multi-archive mode'
                          }
                        >
                          <ArchiveOutlinedIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    ) : null}
                  </Box>

                  {/* Mobile: subscribe + overflow menu */}
                  <Box
                    sx={{
                      display: { xs: 'flex', sm: 'none' },
                      alignItems: 'center',
                      gap: 0.25,
                    }}
                  >
                    <SubscribeButton
                      entityType={3}
                      entityId={board.id}
                      iconButtonProps={{
                        size: 'medium',
                        sx: { minWidth: 44, minHeight: 44 },
                      }}
                    />
                    {(canManageTags || canManageBoards) ? (
                      <>
                        <IconButton
                          size="medium"
                          onClick={(e) => setOverflowMenuAnchor(e.currentTarget)}
                          aria-label="More board actions"
                          sx={{ minWidth: 44, minHeight: 44 }}
                        >
                          <MoreVertIcon />
                        </IconButton>
                        <Menu
                          anchorEl={overflowMenuAnchor}
                          open={Boolean(overflowMenuAnchor)}
                          onClose={() => setOverflowMenuAnchor(null)}
                          anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
                          transformOrigin={{ vertical: 'top', horizontal: 'right' }}
                        >
                          {canManageTags ? (
                            <MenuItem
                              onClick={() => {
                                setTagDialogOpen(true)
                                setOverflowMenuAnchor(null)
                              }}
                            >
                              <ListItemIcon>
                                <LabelOutlinedIcon fontSize="small" />
                              </ListItemIcon>
                              <ListItemText>Manage tags</ListItemText>
                            </MenuItem>
                          ) : null}
                          {canManageBoards ? (
                            <MenuItem
                              onClick={() => {
                                if (selectionMode) {
                                  setSelectedCardIds(new Set())
                                }
                                setSelectionMode((prev) => !prev)
                                setOverflowMenuAnchor(null)
                              }}
                            >
                              <ListItemIcon>
                                <ArchiveOutlinedIcon
                                  fontSize="small"
                                  color={selectionMode ? 'primary' : undefined}
                                />
                              </ListItemIcon>
                              <ListItemText>
                                {selectionMode ? 'Exit multi-archive' : 'Multi-archive tasks'}
                              </ListItemText>
                            </MenuItem>
                          ) : null}
                        </Menu>
                      </>
                    ) : null}
                  </Box>
                </Stack>
              </Stack>

              {canManageBoards && archiveBoardMutation.isError ? (
                <Alert severity="error" sx={{ py: 0.25 }}>
                  Unable to archive board.
                </Alert>
              ) : null}

              {updateBoardMutation.isError ? (
                <Alert severity="error" sx={{ py: 0.25 }}>
                  Unable to update board name.
                </Alert>
              ) : null}
            </Stack>

            {isBoardLoading ? <BoardSkeleton /> : null}

            {columnsQuery.isError ? <Alert severity="error">Unable to load lists.</Alert> : null}

            {cardsQuery.isError ? <Alert severity="error">Unable to load tasks.</Alert> : null}

            {hasActiveBackendFilters && filteredCardsQuery.isError ? (
              <Alert severity="error">Unable to apply filters.</Alert>
            ) : null}

            {tagsQuery.isError ? <Alert severity="error">Unable to load tags.</Alert> : null}

            {updateColumnMutation.isError ? (
              <Alert severity="error">Unable to update list name.</Alert>
            ) : null}

            {archiveColumnMutation.isError ? (
              <Alert severity="error">Unable to archive list.</Alert>
            ) : null}

            {!isBoardLoading ? (
              <DndContext
                sensors={sensors}
                collisionDetection={kanbanCollisionDetection}
                // Built-in autoScroll fights iOS momentum scrolling + CSS
                // scroll-snap on the columns rail. We drive horizontal panning
                // manually in handleDragMove; keep vertical autoScroll for
                // intra-column lists.
                autoScroll={{ threshold: { x: 0, y: 0.15 } }}
                onDragStart={handleDragStart}
                onDragMove={handleDragMove}
                onDragCancel={handleDragCancel}
                onDragOver={handleDragOver}
                onDragEnd={handleDragEnd}
              >
                <Box
                  ref={columnsScrollRef}
                  onScroll={handleColumnsScroll}
                  sx={{
                    overflowX: 'auto',
                    pb: { xs: 0.5, sm: 1 },
                    mx: 0,
                    px: 0,
                    minWidth: 0,
                    // Container query so children can reference the visible scroll-area width.
                    containerType: 'inline-size',
                    // Mobile: snap-scroll one column at a time, but disable while dragging so
                    // dnd-kit's smooth edge auto-scroll can pan the board (Trello-style) without
                    // mandatory-snap jittering each frame.
                    scrollSnapType: isDragActive
                      ? 'none'
                      : { xs: 'x mandatory', sm: 'none' },
                    // iOS momentum scrolling also fights dnd-kit's autoScroll mid-drag.
                    WebkitOverflowScrolling: isDragActive ? 'auto' : 'touch',
                  }}
                >
                  <Stack
                    direction="row"
                    spacing={{ xs: 1, sm: 2 }}
                    alignItems="flex-start"
                    sx={{
                      minWidth: 'min-content',
                    }}
                  >
                    {plannerOpen && canUsePlanner ? (
                      <Box
                        data-board-rail-item
                        sx={{
                          flex: { xs: '0 0 calc(100cqw - 24px)', sm: `0 0 ${plannerWidth}px` },
                          height: { xs: 'calc(100vh - 220px)', sm: 'calc(100vh - 160px)' },
                          borderRadius: 2,
                          border: 1,
                          borderColor: 'divider',
                          overflow: 'hidden',
                          flexShrink: 0,
                          position: 'relative',
                          scrollSnapAlign: { xs: 'start', sm: 'unset' },
                        }}
                      >
                        <BoardPlannerPanel
                          ref={plannerRef}
                          projectId={projectId}
                          onClose={() => setPlannerOpen(false)}
                          dropPreview={plannerDropPreview}
                          onBlockClick={(cardId) => {
                            void navigate({
                              to: '/projects/$projectId/boards/$boardId',
                              params: { projectId, boardId },
                              search: { ...filterSearchParams, cardId },
                            })
                          }}
                        />
                        {/* Resize handle (desktop only) */}
                        <Box
                          onMouseDown={handlePlannerResizeStart}
                          sx={{
                            display: { xs: 'none', sm: 'block' },
                            position: 'absolute',
                            top: 0,
                            right: -4,
                            width: 8,
                            height: '100%',
                            cursor: 'col-resize',
                            zIndex: 10,
                            '&:hover > div, &:active > div': {
                              opacity: 1,
                            },
                          }}
                        >
                          <Box
                            sx={{
                              position: 'absolute',
                              top: 0,
                              left: 3,
                              width: 2,
                              height: '100%',
                              borderRadius: 1,
                              bgcolor: 'primary.main',
                              opacity: 0,
                            }}
                          />
                        </Box>
                      </Box>
                    ) : null}
                    <SortableContext
                      items={orderedColumns.map((column) => column.id)}
                      strategy={horizontalListSortingStrategy}
                    >
                      <>
                        {orderedColumns.map((column) => {
                          const draftName = (editColumnNames[column.id] ?? '').trim()
                          const hasChangedColumnName = draftName !== column.name
                          const canSaveColumnName =
                            canManageBoards &&
                            draftName.length > 0 &&
                            hasChangedColumnName &&
                            !updateColumnMutation.isPending

                          return (
                            <SortableColumnCard
                              key={column.id}
                              column={column}
                              cards={displayCardsByColumn.get(column.id) ?? []}
                              draggingCardId={activeDragCard?.id ?? null}
                              draftName={editColumnNames[column.id] ?? column.name}
                              canManageColumns={canManageBoards}
                              isBusy={
                                updateColumnMutation.isPending || reorderColumnsMutation.isPending
                              }
                              hasChangedName={hasChangedColumnName}
                              canSaveName={canSaveColumnName}
                              selectedCardIds={
                                canManageBoards && selectionMode ? selectedCardIds : undefined
                              }
                              onToggleSelectCard={
                                canManageBoards && selectionMode
                                  ? handleToggleSelectCard
                                  : undefined
                              }
                              onDraftNameChange={(value) =>
                                setEditColumnNames((previous) => ({
                                  ...previous,
                                  [column.id]: value,
                                }))
                              }
                              onSaveName={() => handleSaveColumnName(column)}
                              onArchive={async () => {
                                if (!canManageBoards || archiveColumnMutation.isPending) return
                                await archiveColumnMutation.mutateAsync(column.id)
                              }}
                              isArchiving={archiveColumnMutation.isPending}
                              createCardDraft={createCardDraftByColumn[column.id] ?? ''}
                              createCardError={createCardErrorByColumn[column.id] ?? null}
                              isCreatingCard={createCardMutation.isPending}
                              onCreateCardDraftChange={(value) =>
                                handleCreateCardDraftChange(column.id, value)
                              }
                              onCreateCardSubmit={() => void handleSubmitInlineCard(column.id)}
                              onCreateCardBlur={() =>
                                void handleSubmitInlineCard(column.id, { fromBlur: true })
                              }
                              onArchiveCard={(card) => setArchiveCardTarget(card)}
                              onCardClick={(card) => {
                                void navigate({
                                  to: '/projects/$projectId/boards/$boardId',
                                  params: {
                                    projectId,
                                    boardId,
                                  },
                                  search: {
                                    ...filterSearchParams,
                                    cardId: card.id,
                                  },
                                })
                              }}
                            />
                          )
                        })}

                        {canManageBoards ? (
                          isAddingColumn ? (
                            <Box
                              data-board-rail-item
                              sx={{
                                flex: { xs: '0 0 calc(100cqw - 24px)', sm: '0 0 300px' },
                                borderRadius: 2,
                                bgcolor: 'background.paper',
                                border: '1px solid',
                                borderColor: 'divider',
                                px: 0.75,
                                py: 1.25,
                                scrollSnapAlign: { xs: 'start', sm: 'unset' },
                              }}
                            >
                              <Stack spacing={1.5}>
                                <TextField
                                  autoFocus
                                  size="small"
                                  placeholder="List name"
                                  value={createColumnName}
                                  onChange={(e) => setCreateColumnName(e.target.value)}
                                  onKeyDown={(e) => {
                                    if (e.key === 'Enter' && canCreateColumn) {
                                      void handleCreateColumn().then(() => setIsAddingColumn(false))
                                    }
                                    if (e.key === 'Escape') {
                                      setCreateColumnName('')
                                      setIsAddingColumn(false)
                                    }
                                  }}
                                  fullWidth
                                />
                                <Stack direction="row" spacing={1}>
                                  <Button
                                    size="small"
                                    variant="contained"
                                    disabled={!canCreateColumn}
                                    onClick={() =>
                                      void handleCreateColumn().then(() => setIsAddingColumn(false))
                                    }
                                  >
                                    Add
                                  </Button>
                                  <IconButton
                                    size="small"
                                    aria-label="Cancel adding list"
                                    onClick={() => {
                                      setCreateColumnName('')
                                      setIsAddingColumn(false)
                                    }}
                                  >
                                    <CloseIcon fontSize="small" />
                                  </IconButton>
                                </Stack>
                              </Stack>
                            </Box>
                          ) : (
                            <Box
                              data-board-rail-item
                              onClick={() => setIsAddingColumn(true)}
                              sx={{
                                flex: { xs: '0 0 calc(100cqw - 24px)', sm: '0 0 300px' },
                                borderRadius: 2,
                                border: '2px dashed',
                                borderColor: 'divider',
                                px: 0.75,
                                py: { xs: 2, sm: 1.25 },
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                cursor: 'pointer',
                                opacity: 0.6,
                                scrollSnapAlign: { xs: 'start', sm: 'unset' },
                                minHeight: { xs: 56, sm: 'auto' },
                                '&:hover': {
                                  opacity: 1,
                                  borderColor: 'primary.main',
                                  bgcolor: 'action.hover',
                                },
                              }}
                            >
                              <Stack direction="row" spacing={1} alignItems="center">
                                <AddIcon fontSize="small" />
                                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                                  Add list
                                </Typography>
                              </Stack>
                            </Box>
                          )
                        ) : null}
                      </>
                    </SortableContext>
                  </Stack>

                  <DragOverlay dropAnimation={null}>
                      {activeDragCard ? (
                        <Card
                          elevation={3}
                          sx={{
                            width: 300,
                            borderRadius: 1,
                            border: '1px solid',
                            borderColor: 'primary.main',
                            bgcolor: 'background.paper',
                            transform: 'rotate(2deg)',
                            cursor: 'grabbing',
                            pointerEvents: 'none',
                          }}
                        >
                          <CardContent sx={{ p: 1, '&:last-child': { pb: 1 } }}>
                            <Stack spacing={0.75}>
                              <Typography
                                variant="body2"
                                sx={{
                                  fontWeight: 600,
                                  wordBreak: 'break-word',
                                  fontSize: '0.875rem',
                                }}
                              >
                                {activeDragCard.title}
                              </Typography>
                              {activeDragCard.description ? (
                                <Typography
                                  variant="caption"
                                  color="text.secondary"
                                  noWrap
                                  sx={{ lineHeight: 1.4 }}
                                >
                                  {activeDragCard.description}
                                </Typography>
                              ) : null}
                            </Stack>
                          </CardContent>
                        </Card>
                      ) : null}
                      {activeDragColumn ? (
                        <Box
                          sx={{
                            width: 300,
                            borderRadius: 2,
                            bgcolor: 'background.paper',
                            border: '1px solid',
                            borderColor: 'primary.main',
                            px: 0.75,
                            py: 1.25,
                            boxShadow: 3,
                            transform: 'scale(1.02) rotate(2deg)',
                            cursor: 'grabbing',
                            opacity: 0.95,
                          }}
                        >
                          <Stack direction="row" spacing={1.25} alignItems="center">
                            <Typography
                              variant="body2"
                              sx={{
                                fontWeight: 700,
                                textTransform: 'uppercase',
                                letterSpacing: '0.08em',
                                fontSize: '0.8125rem',
                              }}
                            >
                              {activeDragColumn.name}
                            </Typography>
                            <Chip
                              label={String(
                                displayCardsByColumn.get(activeDragColumn.id)?.length ?? 0,
                              )}
                              size="small"
                              sx={{
                                height: 22,
                                minWidth: 32,
                                fontWeight: 700,
                                fontSize: '0.75rem',
                                borderRadius: 1,
                              }}
                            />
                          </Stack>
                        </Box>
                      ) : null}
                      {activeDragBlock ? (
                        <PlannerTimeBlock block={activeDragBlock} isDragOverlay />
                      ) : null}
                    </DragOverlay>
                </Box>
              </DndContext>
            ) : null}

            {!columnsQuery.isLoading ? (
              <Portal>
                <BoardScrollbar scrollRef={columnsScrollRef} />
              </Portal>
            ) : null}

            {!columnsQuery.isLoading && orderedColumns.length === 0 && !canManageBoards ? (
              <Typography color="text.secondary">No lists yet.</Typography>
            ) : null}

          </Stack>
        ) : null}

        {/* Mobile column pager dots, fixed at bottom of viewport */}
        {board ? (() => {
          const totalRailItems =
            orderedColumns.length +
            (plannerOpen && canUsePlanner ? 1 : 0) +
            (canManageBoards ? 1 : 0)
          if (totalRailItems <= 1) return null
          const bulkBarActive = canManageBoards && selectionMode
          return (
            <Box
              sx={{
                display: { xs: 'flex', sm: 'none' },
                position: 'fixed',
                bottom: bulkBarActive
                  ? 'calc(env(safe-area-inset-bottom, 0px) + 84px)'
                  : 'calc(env(safe-area-inset-bottom, 0px) + 12px)',
                left: '50%',
                transform: 'translateX(-50%)',
                justifyContent: 'center',
                alignItems: 'center',
                px: 1,
                py: 0.25,
                zIndex: 20,
                borderRadius: 999,
                bgcolor: 'background.paper',
                border: 1,
                borderColor: 'divider',
                boxShadow: 3,
                maxWidth: 'calc(100vw - 24px)',
                transition: 'bottom 200ms ease-out',
              }}
              aria-label="Board columns navigation"
            >
              <Stack
                direction="row"
                spacing={0.25}
                justifyContent="center"
                alignItems="center"
                sx={{ flexWrap: 'nowrap' }}
              >
                {Array.from({ length: totalRailItems }).map((_, idx) => {
                  const isActive = idx === activeColumnIndex
                  return (
                    <Box
                      key={idx}
                      component="button"
                      type="button"
                      onClick={() => scrollToRailItem(idx)}
                      aria-label={`Go to item ${idx + 1}`}
                      aria-current={isActive ? 'true' : undefined}
                      sx={{
                        appearance: 'none',
                        background: 'none',
                        border: 'none',
                        cursor: 'pointer',
                        p: 0.5,
                        m: 0,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        minWidth: 20,
                        minHeight: 20,
                        '& > span': {
                          display: 'block',
                          width: isActive ? 14 : 5,
                          height: 5,
                          borderRadius: 3,
                          bgcolor: isActive ? 'primary.main' : 'action.disabled',
                          transition: 'all 150ms',
                        },
                      }}
                    >
                      <span />
                    </Box>
                  )
                })}
              </Stack>
            </Box>
          )
        })() : null}

        <CardDetailDialog
          open={Boolean(selectedCardId)}
          cardId={selectedCardId}
          boardId={boardId}
          columns={columns}
          members={members}
          canManageCards={canManageBoards}
          currentUserRole={currentUserRole}
          onClose={() => {
            void navigate({
              to: '/projects/$projectId/boards/$boardId',
              params: {
                projectId,
                boardId,
              },
              search: {
                ...filterSearchParams,
              },
            })
          }}
        />

        <TagManagementDialog
          open={tagDialogOpen && canManageTags}
          boardId={boardId}
          canManageTags={canManageTags}
          onClose={() => setTagDialogOpen(false)}
        />

        <Dialog
          open={archiveDialogOpen && canManageBoards}
          onClose={() => {
            if (archiveBoardMutation.isPending) {
              return
            }

            setArchiveDialogOpen(false)
          }}
          fullWidth
          maxWidth="xs"
        >
          <DialogTitle>Archive Board</DialogTitle>
          <DialogContent>
            <DialogContentText>
              Are you sure you want to archive this board? You can restore it later from the
              archive.
            </DialogContentText>
          </DialogContent>
          <DialogActions>
            <Button
              onClick={() => setArchiveDialogOpen(false)}
              disabled={archiveBoardMutation.isPending}
            >
              Cancel
            </Button>
            <Button
              onClick={handleArchiveBoard}
              color="warning"
              variant="contained"
              disabled={archiveBoardMutation.isPending}
            >
              Confirm Archive
            </Button>
          </DialogActions>
        </Dialog>

        <Dialog
          open={Boolean(archiveCardTarget) && canManageBoards}
          onClose={() => {
            if (archiveCardMutation.isPending) {
              return
            }

            setArchiveCardTarget(null)
          }}
          fullWidth
          maxWidth="xs"
        >
          <DialogTitle>Archive Task</DialogTitle>
          <DialogContent>
            <DialogContentText>
              Are you sure you want to archive
              {archiveCardTarget ? ` “${archiveCardTarget.title}”` : ''}? You can restore it later
              from the archive.
            </DialogContentText>
          </DialogContent>
          <DialogActions>
            <Button
              onClick={() => setArchiveCardTarget(null)}
              disabled={archiveCardMutation.isPending}
            >
              Cancel
            </Button>
            <Button
              onClick={handleArchiveCard}
              color="warning"
              variant="contained"
              disabled={archiveCardMutation.isPending}
            >
              Confirm Archive
            </Button>
          </DialogActions>
        </Dialog>

        {/* Bulk action bar */}
        {canManageBoards && selectionMode ? (
          <Box
            sx={{
              position: 'fixed',
              bottom: { xs: 'calc(env(safe-area-inset-bottom, 0px) + 16px)', sm: 24 },
              left: '50%',
              transform: 'translateX(-50%)',
              zIndex: 1300,
              bgcolor: 'background.paper',
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 3,
              boxShadow: 6,
              px: 2.5,
              py: 1.25,
              maxWidth: 'calc(100vw - 24px)',
            }}
          >
            <Stack direction="row" spacing={2} alignItems="center">
              <Typography variant="body2" sx={{ fontWeight: 600, whiteSpace: 'nowrap' }}>
                {selectedCardIds.size} selected
              </Typography>
              <Button
                size="small"
                variant="contained"
                color="warning"
                startIcon={<ArchiveOutlinedIcon />}
                disabled={isBulkArchiving || selectedCardIds.size === 0}
                onClick={handleBulkArchive}
              >
                {isBulkArchiving ? 'Archiving...' : 'Archive'}
              </Button>
              <Button
                size="small"
                variant="outlined"
                onClick={() => {
                  setSelectedCardIds(new Set())
                  setSelectionMode(false)
                }}
                disabled={isBulkArchiving}
              >
                Cancel
              </Button>
            </Stack>
          </Box>
        ) : null}

        <Drawer anchor="right" open={archiveDrawerOpen} onClose={() => setArchiveDrawerOpen(false)}>
          <Box sx={{ width: { xs: '100vw', sm: 380 }, maxWidth: '100vw', p: 2.5 }}>
            <Stack spacing={2}>
              <Stack direction="row" alignItems="center" justifyContent="space-between">
                <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  Board Archive
                </Typography>
                <IconButton
                  size="small"
                  onClick={() => setArchiveDrawerOpen(false)}
                  aria-label="Close archive drawer"
                >
                  <CloseIcon fontSize="small" />
                </IconButton>
              </Stack>

              <Tabs
                value={archiveDrawerTab}
                onChange={(_, value: 'columns' | 'cards') => setArchiveDrawerTab(value)}
                variant="fullWidth"
              >
                <Tab value="columns" label={`Lists (${archivedColumnsQuery.data?.length ?? 0})`} />
                <Tab value="cards" label={`Tasks (${archivedCardsQuery.data?.totalCount ?? 0})`} />
              </Tabs>

              <Divider />

              {archiveDrawerTab === 'columns' ? (
                <ArchiveDrawerList
                  isLoading={archivedColumnsQuery.isLoading}
                  isError={archivedColumnsQuery.isError}
                  emptyText="No archived lists."
                  hasItems={(archivedColumnsQuery.data?.length ?? 0) > 0}
                >
                  <List disablePadding>
                    {(archivedColumnsQuery.data ?? []).map((column) => (
                      <ListItem
                        key={column.id}
                        secondaryAction={
                          canManageBoards ? (
                            <Stack direction="row" spacing={0.5}>
                              <Tooltip title="Restore list">
                                <IconButton
                                  edge="end"
                                  size="small"
                                  onClick={() => restoreColumnMutation.mutate(column.id)}
                                  disabled={restoreColumnMutation.isPending || purgeColumnMutation.isPending}
                                  aria-label={`Restore list ${column.name}`}
                                >
                                  <RestoreIcon fontSize="small" />
                                </IconButton>
                              </Tooltip>
                              <Tooltip title="Delete permanently">
                                <IconButton
                                  edge="end"
                                  size="small"
                                  color="error"
                                  onClick={() => purgeColumnMutation.mutate(column.id)}
                                  disabled={restoreColumnMutation.isPending || purgeColumnMutation.isPending}
                                  aria-label={`Delete list ${column.name} permanently`}
                                >
                                  <DeleteForeverIcon fontSize="small" />
                                </IconButton>
                              </Tooltip>
                            </Stack>
                          ) : null
                        }
                        sx={{
                          borderRadius: 2,
                          mb: 0.5,
                          pr: canManageBoards ? 10 : 2,
                          '& .MuiListItemSecondaryAction-root': { right: 4 },
                        }}
                      >
                        <ListItemText
                          primary={column.name}
                          secondary={
                            column.deletedAt
                              ? `Archived ${formatDeletedAt(column.deletedAt)}`
                              : undefined
                          }
                          primaryTypographyProps={{ variant: 'body2', fontWeight: 600 }}
                          secondaryTypographyProps={{ variant: 'caption' }}
                        />
                      </ListItem>
                    ))}
                  </List>
                </ArchiveDrawerList>
              ) : null}

              {archiveDrawerTab === 'cards' ? (
                <ArchiveDrawerList
                  isLoading={archivedCardsQuery.isLoading}
                  isError={archivedCardsQuery.isError}
                  emptyText="No archived tasks."
                  hasItems={(archivedCardsQuery.data?.items.length ?? 0) > 0}
                >
                  <List disablePadding>
                    {(archivedCardsQuery.data?.items ?? []).map((card) => {
                      const listArchived = archivedColumnIds.has(card.columnId)
                      return (
                      <ListItem
                        key={card.id}
                        secondaryAction={
                          canManageBoards ? (
                            <Stack direction="row" spacing={0.5}>
                              <Tooltip
                                title={
                                  listArchived
                                    ? 'Restore the list first to unarchive this task'
                                    : 'Restore task'
                                }
                              >
                                <span>
                                  <IconButton
                                    edge="end"
                                    size="small"
                                    onClick={() => restoreCardMutation.mutate(card.id)}
                                    disabled={
                                      listArchived ||
                                      restoreCardMutation.isPending ||
                                      purgeCardMutation.isPending
                                    }
                                    aria-label={`Restore task ${card.title}`}
                                  >
                                    <RestoreIcon fontSize="small" />
                                  </IconButton>
                                </span>
                              </Tooltip>
                              <Tooltip title="Delete permanently">
                                <IconButton
                                  edge="end"
                                  size="small"
                                  color="error"
                                  onClick={() => purgeCardMutation.mutate(card.id)}
                                  disabled={restoreCardMutation.isPending || purgeCardMutation.isPending}
                                  aria-label={`Delete task ${card.title} permanently`}
                                >
                                  <DeleteForeverIcon fontSize="small" />
                                </IconButton>
                              </Tooltip>
                            </Stack>
                          ) : null
                        }
                        sx={{
                          borderRadius: 2,
                          mb: 0.5,
                          pr: canManageBoards ? 10 : 2,
                          '& .MuiListItemSecondaryAction-root': { right: 4 },
                        }}
                      >
                        <ListItemText
                          primary={card.title}
                          secondary={[
                            card.column?.name ? `List: ${card.column.name}` : null,
                            card.deletedAt ? `Archived ${formatDeletedAt(card.deletedAt)}` : null,
                          ]
                            .filter(Boolean)
                            .join(' · ')}
                          primaryTypographyProps={{ variant: 'body2', fontWeight: 600 }}
                          secondaryTypographyProps={{ variant: 'caption' }}
                        />
                      </ListItem>
                      )
                    })}
                  </List>
                </ArchiveDrawerList>
              ) : null}
            </Stack>
          </Box>
        </Drawer>

      </Stack>
    </Box>
  )
}

type SortableColumnCardProps = {
  column: Column
  cards: BoardCard[]
  draggingCardId: string | null
  draftName: string
  canManageColumns: boolean
  isBusy: boolean
  hasChangedName: boolean
  canSaveName: boolean
  selectedCardIds?: Set<Guid>
  onToggleSelectCard?: (cardId: Guid) => void
  onDraftNameChange: (value: string) => void
  onSaveName: () => void
  onArchive: () => Promise<void>
  isArchiving: boolean
  createCardDraft: string
  createCardError: string | null
  isCreatingCard: boolean
  onCreateCardDraftChange: (value: string) => void
  onCreateCardSubmit: () => void
  onCreateCardBlur: () => void
  onArchiveCard: (card: BoardCard) => void
  onCardClick: (card: BoardCard) => void
}

function SortableColumnCard({
  column,
  cards,
  draggingCardId,
  draftName,
  canManageColumns,
  isBusy,
  hasChangedName,
  canSaveName,
  selectedCardIds,
  onToggleSelectCard,
  onDraftNameChange,
  onSaveName,
  onArchive,
  isArchiving,
  createCardDraft,
  createCardError,
  isCreatingCard,
  onCreateCardDraftChange,
  onCreateCardSubmit,
  onCreateCardBlur,
  onArchiveCard,
  onCardClick,
}: SortableColumnCardProps) {
  const [isEditingName, setIsEditingName] = useState(false)
  const [showAddCard, setShowAddCard] = useState(false)
  const [showArchiveConfirm, setShowArchiveConfirm] = useState(false)

  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: column.id,
    data: {
      type: 'column',
      columnId: column.id,
    },
    disabled: !canManageColumns || isBusy,
  })

  const { isOver: isColumnDropOver, setNodeRef: setDroppableRef } = useDroppable({
    id: `column-drop-${column.id}`,
    data: {
      type: 'column-drop',
      columnId: column.id,
    },
  })

  const setRefs = useCallback(
    (node: HTMLElement | null) => {
      setNodeRef(node)
      setDroppableRef(node)
    },
    [setNodeRef, setDroppableRef],
  )

  return (
    <Box
      ref={setRefs}
      data-column-id={column.id}
      sx={{
        flex: { xs: '0 0 calc(100cqw - 24px)', sm: '0 0 300px' },
        minWidth: 0,
        overflow: 'hidden',
        scrollSnapAlign: { xs: 'start', sm: 'unset' },
        borderRadius: 2,
        bgcolor: (theme) =>
          theme.palette.mode === 'dark' ? 'rgba(255,255,255,0.03)' : 'rgba(220,230,245,0.35)',
        px: { xs: 0.5, sm: 0.75 },
        py: { xs: 1, sm: 1.25 },
        transform: CSS.Transform.toString(transform),
        transition,
        '&:hover .column-drag-handle': {
          opacity: 1,
        },
        ...(isDragging
          ? {
              border: '2px dashed',
              borderColor: 'primary.main',
            }
          : isColumnDropOver
            ? {
                border: '1px solid',
                borderColor: 'primary.main',
                bgcolor: (theme) =>
                  theme.palette.mode === 'dark' ? 'rgba(20,184,166,0.08)' : 'rgba(13,148,136,0.08)',
              }
            : {
                border: '1px solid',
                borderColor: (theme) =>
                  theme.palette.mode === 'dark'
                    ? 'rgba(255,255,255,0.06)'
                    : 'rgba(190,210,235,0.5)',
              }),
      }}
    >
      <Stack spacing={1.5} sx={{ visibility: isDragging ? 'hidden' : 'visible' }}>
        {/* Column header */}
        <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ px: 0.25 }}>
          <Stack direction="row" spacing={1} alignItems="center" sx={{ minWidth: 0, flex: 1 }}>
            {/* Drag handle */}
            {canManageColumns ? (
              <Box
                className="column-drag-handle"
                {...attributes}
                {...listeners}
                sx={{
                  display: 'flex',
                  alignItems: 'center',
                  cursor: 'grab',
                  opacity: 0,
                  color: 'text.disabled',
                  flexShrink: 0,
                  '&:hover': { color: 'text.secondary' },
                }}
              >
                <DragIndicatorIcon sx={{ fontSize: 18 }} />
              </Box>
            ) : null}

            {isEditingName && canManageColumns ? (
              <TextField
                value={draftName}
                onChange={(event) => onDraftNameChange(event.target.value)}
                onBlur={() => {
                  if (hasChangedName && canSaveName) {
                    onSaveName()
                  }
                  setIsEditingName(false)
                }}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault()
                    if (hasChangedName && canSaveName) {
                      onSaveName()
                    }
                    setIsEditingName(false)
                  }
                  if (event.key === 'Escape') {
                    event.preventDefault()
                    onDraftNameChange(column.name)
                    setIsEditingName(false)
                  }
                }}
                size="small"
                autoFocus
                disabled={isBusy}
                sx={{
                  flex: 1,
                  '& .MuiInputBase-input': {
                    fontWeight: 700,
                    fontSize: '0.8125rem',
                    textTransform: 'uppercase',
                    letterSpacing: '0.08em',
                    py: 0.5,
                    px: 1,
                  },
                }}
              />
            ) : (
              <Typography
                variant="body2"
                onClick={() => {
                  if (canManageColumns) {
                    setIsEditingName(true)
                  }
                }}
                noWrap
                sx={{
                  fontWeight: 700,
                  textTransform: 'uppercase',
                  letterSpacing: '0.08em',
                  fontSize: '0.8125rem',
                  lineHeight: 1.4,
                  cursor: canManageColumns ? 'text' : 'default',
                  flex: 1,
                  minWidth: 0,
                }}
              >
                {column.name}
              </Typography>
            )}

            <Chip
              label={String(cards.length)}
              size="small"
              sx={{
                height: 22,
                minWidth: 28,
                fontWeight: 700,
                fontSize: '0.75rem',
                lineHeight: 1,
                borderRadius: 1,
                bgcolor: cards.length > 0 ? 'primary.main' : 'action.selected',
                color: cards.length > 0 ? 'primary.contrastText' : 'text.secondary',
                '& .MuiChip-label': { lineHeight: 1, display: 'flex', alignItems: 'center' },
              }}
            />
          </Stack>

          <Stack direction="row" spacing={0.25} alignItems="center" sx={{ flexShrink: 0 }}>
            <SubscribeButton
              entityType={1}
              entityId={column.id}
              iconButtonProps={{
                size: 'small',
                sx: { minWidth: { xs: 40, sm: 'auto' }, minHeight: { xs: 40, sm: 'auto' } },
              }}
            />
            {canManageColumns ? (
              <IconButton
                size="small"
                onClick={(event) => {
                  event.stopPropagation()
                  setShowArchiveConfirm(true)
                }}
                onPointerDown={(event) => event.stopPropagation()}
                disabled={isBusy}
                aria-label={`Archive ${column.name}`}
                sx={{ minWidth: { xs: 40, sm: 'auto' }, minHeight: { xs: 40, sm: 'auto' } }}
              >
                <ArchiveOutlinedIcon sx={{ fontSize: 18 }} />
              </IconButton>
            ) : null}
          </Stack>
        </Stack>

        {/* Inline archive confirmation */}
        {showArchiveConfirm ? (
          <Alert
            severity="warning"
            sx={{
              py: 1,
              px: 1.5,
              borderRadius: 2,
              '& .MuiAlert-message': { width: '100%', py: 0 },
              '& .MuiAlert-icon': { mr: 1, py: 0, alignItems: 'center' },
            }}
          >
            <Stack spacing={1} sx={{ width: '100%' }}>
              <Typography
                variant="caption"
                sx={{ wordBreak: 'break-word', lineHeight: 1.4 }}
              >
                Archive &quot;{column.name}&quot;?
              </Typography>
              <Stack direction="row" spacing={1} justifyContent="flex-end">
                <Button
                  size="small"
                  onClick={() => setShowArchiveConfirm(false)}
                  disabled={isArchiving}
                  sx={{ textTransform: 'none', fontSize: '0.75rem', px: 1.25 }}
                >
                  Cancel
                </Button>
                <Button
                  size="small"
                  color="warning"
                  variant="contained"
                  onClick={async () => {
                    await onArchive()
                    setShowArchiveConfirm(false)
                  }}
                  disabled={isArchiving}
                  sx={{ textTransform: 'none', fontSize: '0.75rem', px: 1.25 }}
                >
                  Archive
                </Button>
              </Stack>
            </Stack>
          </Alert>
        ) : null}

        {/* Card list area */}
        <Box
          sx={{
            borderRadius: 2,
            border: draggingCardId && isColumnDropOver ? '2px dashed' : '2px solid transparent',
            borderColor: draggingCardId && isColumnDropOver ? 'primary.main' : 'transparent',
            bgcolor:
              draggingCardId && isColumnDropOver
                ? (theme) =>
                    theme.palette.mode === 'dark'
                      ? 'rgba(20,184,166,0.06)'
                      : 'rgba(13,148,136,0.04)'
                : 'transparent',
            p: { xs: 0, sm: 0.25 },
            minHeight: 48,
          }}
        >
          <CardList
            columnId={column.id}
            cards={cards}
            draggingCardId={draggingCardId}
            canArchiveCards={canManageColumns}
            selectedCardIds={selectedCardIds}
            onToggleSelectCard={onToggleSelectCard}
            onArchiveCard={onArchiveCard}
            onCardClick={onCardClick}
          />
        </Box>

        {/* Add task section */}
        {canManageColumns ? (
          showAddCard || createCardDraft.length > 0 ? (
            <Stack spacing={0.75}>
              <TextField
                placeholder="Enter a title..."
                value={createCardDraft}
                onChange={(event) => onCreateCardDraftChange(event.target.value)}
                onBlur={() => {
                  onCreateCardBlur()
                  if (!createCardDraft.trim()) {
                    setShowAddCard(false)
                  }
                }}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault()
                    onCreateCardSubmit()
                    return
                  }
                  if (event.key === 'Escape') {
                    event.preventDefault()
                    onCreateCardDraftChange('')
                    setShowAddCard(false)
                  }
                }}
                size="small"
                fullWidth
                autoFocus
                disabled={isCreatingCard}
                sx={{
                  '& .MuiInputBase-input': {
                    fontSize: { xs: '1rem', sm: '0.875rem' },
                    py: { xs: 1.25, sm: 1 },
                  },
                }}
              />
              {createCardError ? (
                <Alert severity="error" sx={{ py: 0.25, px: 1 }}>
                  {createCardError}
                </Alert>
              ) : null}
            </Stack>
          ) : (
            <Button
              variant="text"
              startIcon={<AddIcon />}
              onClick={() => setShowAddCard(true)}
              sx={{
                justifyContent: 'flex-start',
                color: 'text.secondary',
                fontWeight: 500,
                textTransform: 'none',
                minHeight: { xs: 44, sm: 32 },
                py: { xs: 1, sm: 0.5 },
                fontSize: { xs: '0.9375rem', sm: '0.8125rem' },
                '&:hover': { color: 'primary.main', bgcolor: 'transparent' },
              }}
            >
              Add a task
            </Button>
          )
        ) : null}
      </Stack>
    </Box>
  )
}

function ArchiveDrawerList({
  isLoading,
  isError,
  emptyText,
  hasItems,
  children,
}: {
  isLoading: boolean
  isError: boolean
  emptyText: string
  hasItems: boolean
  children: React.ReactNode
}) {
  if (isLoading) {
    return (
      <Typography color="text.secondary" variant="body2">
        Loading...
      </Typography>
    )
  }

  if (isError) {
    return <Alert severity="error">Unable to load archived items.</Alert>
  }

  if (!hasItems) {
    return (
      <Typography color="text.secondary" variant="body2">
        {emptyText}
      </Typography>
    )
  }

  return <>{children}</>
}

function formatDeletedAt(value?: string | null): string {
  if (!value) {
    return ''
  }

  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }

  const year = parsed.getFullYear()
  const month = String(parsed.getMonth() + 1).padStart(2, '0')
  const day = String(parsed.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function parseCsvGuidList(value: string | undefined): Guid[] {
  if (!value) {
    return []
  }

  return value
    .split(',')
    .map((item) => item.trim())
    .filter((item) => item.length > 0)
}
