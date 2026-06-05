import AddIcon from '@mui/icons-material/Add'
import ArchiveIcon from '@mui/icons-material/Archive'
import CloseIcon from '@mui/icons-material/Close'
import AttachFileIcon from '@mui/icons-material/AttachFile'
import ChatBubbleOutlineIcon from '@mui/icons-material/ChatBubbleOutline'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import CloudUploadIcon from '@mui/icons-material/CloudUpload'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'
import DragIndicatorIcon from '@mui/icons-material/DragIndicator'
import CalendarTodayIcon from '@mui/icons-material/CalendarToday'
import AccessTimeIcon from '@mui/icons-material/AccessTime'
import { DateCalendar } from '@mui/x-date-pickers/DateCalendar'
import { parseISO, format as formatDateFns } from 'date-fns'
import EditIcon from '@mui/icons-material/Edit'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import ExpandLessIcon from '@mui/icons-material/ExpandLess'
import FiberManualRecordIcon from '@mui/icons-material/FiberManualRecord'
import HistoryIcon from '@mui/icons-material/History'
import InsertDriveFileIcon from '@mui/icons-material/InsertDriveFile'
import ImageIcon from '@mui/icons-material/Image'
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdf'
import SendIcon from '@mui/icons-material/Send'
import SubjectIcon from '@mui/icons-material/Subject'
import Alert from '@mui/material/Alert'
import { UserAvatar } from '@/features/auth'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import Divider from '@mui/material/Divider'
import IconButton from '@mui/material/IconButton'
import LinearProgress from '@mui/material/LinearProgress'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import Select from '@mui/material/Select'
import Stack from '@mui/material/Stack'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { AxiosError } from 'axios'
import { Fragment, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { DragEvent, MouseEvent, ReactNode } from 'react'
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import type { DragEndEvent } from '@dnd-kit/core'
import {
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { CardDetailSkeleton } from '@/components/loading/CardDetailSkeleton'
import { SubscribeButton } from '@/features/subscriptions'
import { TagChip } from '@/features/tags'
import {
  useAddAttachment,
  useArchiveCard,
  useAssignTag,
  useAssignUser,
  useCard,
  useCardActivities,
  useCreateSubtask,
  useDeleteSubtask,
  useMoveCard,
  useRemoveAttachment,
  useUnassignTag,
  useUnassignUser,
  useUpdateSubtask,
  useUpdateCard,
  downloadAttachment,
} from '@/features/cards/api/cards'
import { useComments, useCreateComment, useUpdateComment, useDeleteComment } from '@/features/cards/api/comments'
import { useBoardTags } from '@/features/cards/api/tags'
import { useAuthStore } from '@/features/auth'
import type { Attachment, Column, Guid, ProjectMember, Subtask, Tag } from '@/lib/types'
import Popover from '@mui/material/Popover'
import { GoogleDriveLinksSection } from './GoogleDriveLinksSection'

type CardDetailDialogProps = {
  open: boolean
  cardId: Guid | null
  boardId: Guid
  columns?: Column[]
  members: ProjectMember[]
  canManageCards: boolean
  currentUserRole?: number
  onClose: () => void
}

function formatDateShort(dateValue: string) {
  const date = new Date(dateValue)
  if (Number.isNaN(date.getTime())) {
    return dateValue
  }

  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function formatDateTime(dateValue: string) {
  const date = new Date(dateValue)
  if (Number.isNaN(date.getTime())) {
    return dateValue
  }

  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  const hours = String(date.getHours()).padStart(2, '0')
  const minutes = String(date.getMinutes()).padStart(2, '0')
  return `${year}-${month}-${day} ${hours}:${minutes}`
}

const URL_REGEX = /https?:\/\/[^\s<>)"]+/g

function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

function textToHighlightedHtml(text: string): string {
  const escaped = escapeHtml(text)
  return escaped.replace(
    /https?:\/\/[^\s&<>)"]+/g,
    (url) => `<span class="desc-link">${url}</span>`,
  )
}

function getPlainText(el: HTMLElement): string {
  // Walk child nodes to preserve newlines from <br> and block elements
  let result = ''
  for (const node of el.childNodes) {
    if (node.nodeType === Node.TEXT_NODE) {
      result += node.textContent ?? ''
    } else if (node.nodeType === Node.ELEMENT_NODE) {
      const tag = (node as HTMLElement).tagName
      if (tag === 'BR') {
        result += '\n'
      } else if (tag === 'DIV' || tag === 'P') {
        if (result.length > 0 && !result.endsWith('\n')) result += '\n'
        result += getPlainText(node as HTMLElement)
      } else {
        result += getPlainText(node as HTMLElement)
      }
    }
  }
  return result
}

type DescriptionEditorProps = {
  value: string
  onChange: (value: string) => void
  onBlur: () => void
}

function DescriptionEditor({ value, onChange, onBlur }: DescriptionEditorProps) {
  const editorRef = useRef<HTMLDivElement>(null)
  const internalValue = useRef(value)

  useEffect(() => {
    const el = editorRef.current
    if (!el) return
    el.innerHTML = textToHighlightedHtml(value) || '<br>'
    const range = document.createRange()
    const sel = window.getSelection()
    range.selectNodeContents(el)
    range.collapse(false)
    sel?.removeAllRanges()
    sel?.addRange(range)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleInput = useCallback(() => {
    const el = editorRef.current
    if (!el) return
    const plainText = getPlainText(el)
    internalValue.current = plainText
    onChange(plainText)

    const sel = window.getSelection()
    if (!sel || sel.rangeCount === 0) return
    const range = sel.getRangeAt(0)

    const preRange = document.createRange()
    preRange.selectNodeContents(el)
    preRange.setEnd(range.startContainer, range.startOffset)
    const cursorOffset = preRange.toString().length

    el.innerHTML = textToHighlightedHtml(plainText) || '<br>'

    restoreCursor(el, cursorOffset)
  }, [onChange])

  const handlePaste = useCallback((e: React.ClipboardEvent) => {
    e.preventDefault()
    const text = e.clipboardData.getData('text/plain')
    document.execCommand('insertText', false, text)
  }, [])

  return (
    <Box
      ref={editorRef}
      contentEditable
      suppressContentEditableWarning
      onInput={handleInput}
      onBlur={onBlur}
      onPaste={handlePaste}
      role="textbox"
      aria-multiline="true"
      aria-label="Description"
      tabIndex={0}
      sx={{
        minHeight: 72,
        maxHeight: 240,
        overflowY: 'auto',
        p: '8.5px 14px',
        fontSize: '0.875rem',
        lineHeight: 1.4375,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        border: '1px solid',
        borderColor: 'primary.main',
        borderRadius: 1,
        outline: 'none',
        '&:focus': {
          borderColor: 'primary.main',
          boxShadow: (theme) => `0 0 0 1px ${theme.palette.primary.main}`,
        },
        '&:empty::before': {
          content: '"Add a description..."',
          color: 'text.disabled',
        },
        '& .desc-link': {
          color: '#0d9488',
          textDecoration: 'underline',
        },
      }}
    />
  )
}

function restoreCursor(el: HTMLElement, targetOffset: number) {
  const sel = window.getSelection()
  if (!sel) return
  const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT)
  let offset = 0
  let node: Node | null
  while ((node = walker.nextNode())) {
    const len = (node.textContent ?? '').length
    if (offset + len >= targetOffset) {
      const range = document.createRange()
      range.setStart(node, targetOffset - offset)
      range.collapse(true)
      sel.removeAllRanges()
      sel.addRange(range)
      return
    }
    offset += len
  }
}

function linkifyText(text: string): ReactNode {
  const parts: ReactNode[] = []
  let lastIndex = 0
  let match: RegExpExecArray | null
  while ((match = URL_REGEX.exec(text)) !== null) {
    if (match.index > lastIndex) {
      parts.push(text.slice(lastIndex, match.index))
    }
    const url = match[0]
    parts.push(
      <a key={match.index} href={url} target="_blank" rel="noopener noreferrer" style={{ color: '#0d9488', textDecoration: 'underline' }} onClick={(e) => e.stopPropagation()}>
        {url}
      </a>,
    )
    lastIndex = match.index + url.length
  }
  if (lastIndex < text.length) {
    parts.push(text.slice(lastIndex))
  }
  return parts.length > 0 ? parts.map((part, i) => <Fragment key={i}>{part}</Fragment>) : text
}

function formatActivityDescription(action: string, field?: string | null, oldValue?: string | null, newValue?: string | null): string {
  if (action === 'created') return 'created this task'
  if (action === 'archived') return 'archived this task'
  if (action === 'restored') return 'restored this task'

  if (action === 'moved' && field === 'list') {
    return `moved from ${oldValue ?? '?'} to ${newValue ?? '?'}`
  }

  if (action === 'added') {
    if (field === 'comment') return 'added a comment'
    if (field === 'subtask') return `added subtask "${newValue}"`
    if (field === 'tag') return `added tag "${newValue}"`
    if (field === 'assignee') return `assigned ${newValue}`
    if (field === 'attachment') return `attached "${newValue}"`
    if (field === 'google drive') return `linked "${newValue}"`
    return `added ${field ?? 'item'}`
  }

  if (action === 'removed') {
    if (field === 'comment') return 'removed a comment'
    if (field === 'subtask') return `removed subtask "${oldValue}"`
    if (field === 'tag') return `removed tag "${oldValue}"`
    if (field === 'assignee') return `unassigned ${oldValue}`
    if (field === 'attachment') return `removed attachment "${oldValue}"`
    if (field === 'google drive') return `unlinked "${oldValue}"`
    if (field === 'start date') return 'removed start date'
    if (field === 'due date') return 'removed due date'
    if (field === 'estimated hours') return 'removed estimated hours'
    return `removed ${field ?? 'item'}`
  }

  if (action === 'changed') {
    if (field === 'title') return `renamed to "${newValue}"`
    if (field === 'description') return 'updated the description'
    if (field === 'start date') return `set start date to ${newValue}`
    if (field === 'due date') return `set due date to ${newValue}`
    if (field === 'estimated hours') return `set estimated hours to ${newValue}h`
    return `changed ${field ?? 'field'}`
  }

  if (action === 'completed' && field === 'subtask') {
    return `completed subtask "${newValue}"`
  }

  if (action === 'uncompleted' && field === 'subtask') {
    return `uncompleted subtask "${newValue}"`
  }

  return `${action}${field ? ` ${field}` : ''}`
}

function isOverdue(dateValue: string): boolean {
  const date = new Date(dateValue)
  if (Number.isNaN(date.getTime())) return false
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  return date < today
}

const MAX_FILE_SIZE = 25 * 1024 * 1024
const MAX_TOTAL_SIZE_PER_CARD = 100 * 1024 * 1024
const ALLOWED_EXTENSIONS = new Set([
  '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.svg',
  '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx',
  '.txt', '.csv', '.json', '.xml', '.md',
  '.zip', '.rar', '.7z', '.tar', '.gz',
])

function validateFile(file: File, existingTotalBytes: number): string | null {
  if (file.size > MAX_FILE_SIZE) {
    return `File "${file.name}" exceeds the 25 MB limit.`
  }
  if (existingTotalBytes + file.size > MAX_TOTAL_SIZE_PER_CARD) {
    return `Total attachments on this card would exceed the 100 MB limit.`
  }
  const ext = '.' + file.name.split('.').pop()?.toLowerCase()
  if (!ext || !ALLOWED_EXTENSIONS.has(ext)) {
    return `File type "${ext}" is not allowed.`
  }
  return null
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function getMemberLabel(member: ProjectMember): string {
  return member.userName?.trim() || member.user?.userName?.trim() || member.email?.trim() || member.user?.email?.trim() || member.userId
}


type CardDetailEditorProps = {
  card: NonNullable<ReturnType<typeof useCard>['data']>
  columns: Column[]
  tags: Tag[]
  members: ProjectMember[]
  canManageCards: boolean
  currentUserRole?: number
  onClose: () => void
  onRefresh: () => Promise<unknown>
}

function SortableSubtaskItem({
  subtask,
  canManageCards,
  isBusy,
  onToggle,
  onDelete,
  onSaveDescription,
}: {
  subtask: Subtask
  canManageCards: boolean
  isBusy: boolean
  onToggle: (id: Guid, completed: boolean) => void
  onDelete: (id: Guid) => void
  onSaveDescription: (id: Guid, description: string) => void
}) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: subtask.id })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  }

  return (
    <Box
      ref={setNodeRef}
      style={style}
      sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 0.5,
        borderRadius: 1,
        px: 1,
        py: 0.25,
        '&:hover': { bgcolor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.06)' : 'rgba(13, 148, 136, 0.04)' },
        '&:hover .subtask-delete': { opacity: 1 },
      }}
    >
      {canManageCards ? (
        <Box
          {...attributes}
          {...listeners}
          sx={{ display: 'flex', cursor: 'grab', color: 'text.disabled', '&:active': { cursor: 'grabbing' } }}
        >
          <DragIndicatorIcon fontSize="small" />
        </Box>
      ) : null}
      <Checkbox
        checked={subtask.completed}
        onChange={(event) => onToggle(subtask.id, event.target.checked)}
        disabled={!canManageCards || isBusy}
        inputProps={{ 'aria-label': `Toggle subtask ${subtask.description}` }}
        sx={{ color: 'primary.main' }}
      />
      <TextField
        key={`${subtask.id}-${subtask.updatedAt}`}
        size="small"
        defaultValue={subtask.description}
        onBlur={(event) => {
          if (!canManageCards || isBusy) return
          if (!(event.target instanceof HTMLInputElement)) return
          onSaveDescription(subtask.id, event.target.value)
        }}
        onKeyDown={(event) => {
          if (!canManageCards || isBusy) return
          if (!(event.target instanceof HTMLInputElement)) return
          if (event.key === 'Enter') {
            event.preventDefault()
            onSaveDescription(subtask.id, event.target.value)
          }
          if (event.key === 'Escape') {
            event.target.value = subtask.description
            event.target.blur()
          }
        }}
        disabled={!canManageCards || isBusy}
        fullWidth
        variant="standard"
        slotProps={{
          input: {
            disableUnderline: true,
            sx: {
              textDecoration: subtask.completed ? 'line-through' : 'none',
              color: subtask.completed ? 'text.secondary' : 'text.primary',
            },
          },
        }}
      />
      {canManageCards ? (
        <IconButton
          className="subtask-delete"
          color="error"
          size="small"
          onClick={() => onDelete(subtask.id)}
          disabled={isBusy}
          aria-label={`Delete subtask ${subtask.description}`}
          sx={{ opacity: 0, p: 0.25 }}
        >
          <DeleteOutlineIcon sx={{ fontSize: 16 }} />
        </IconButton>
      ) : null}
    </Box>
  )
}

function CardDetailEditor({ card, columns, tags, members, canManageCards, currentUserRole, onClose, onRefresh }: CardDetailEditorProps) {
  const updateCardMutation = useUpdateCard()
  const archiveCardMutation = useArchiveCard()
  const moveCardMutation = useMoveCard()
  const assignTagMutation = useAssignTag()
  const unassignTagMutation = useUnassignTag()
  const assignUserMutation = useAssignUser()
  const unassignUserMutation = useUnassignUser()
  const createSubtaskMutation = useCreateSubtask()
  const updateSubtaskMutation = useUpdateSubtask()
  const deleteSubtaskMutation = useDeleteSubtask()
  const addAttachmentMutation = useAddAttachment()
  const removeAttachmentMutation = useRemoveAttachment()
  const activitiesQuery = useCardActivities(card.id)
  const commentsQuery = useComments(card.id)
  const createCommentMutation = useCreateComment()
  const updateCommentMutation = useUpdateComment()
  const deleteCommentMutation = useDeleteComment()
  const currentUser = useAuthStore(state => state.user)

  const [activeTab, setActiveTab] = useState(0)
  const [descriptionMode, setDescriptionMode] = useState<'edit' | 'preview'>('preview')
  const [titleDraft, setTitleDraft] = useState(card.title)
  const [descriptionDraft, setDescriptionDraft] = useState(card.description ?? '')
  const [startDateDraft, setStartDateDraft] = useState(card.startDate ? formatDateShort(card.startDate) : '')
  const [dueDateDraft, setDueDateDraft] = useState(card.dueDate ? formatDateShort(card.dueDate) : '')
  const [estimatedHoursDraft, setEstimatedHoursDraft] = useState<string>(
    card.estimatedHours != null ? String(card.estimatedHours) : '',
  )
  const [selectedTagIds, setSelectedTagIds] = useState<Guid[]>((card.cardTags ?? []).map((cardTag) => cardTag.tagId))
  const [selectedAssigneeIds, setSelectedAssigneeIds] = useState<Guid[]>((card.assignments ?? []).map((assignment) => assignment.userId))
  const [startDateAnchor, setStartDateAnchor] = useState<HTMLElement | null>(null)
  const [dueDateAnchor, setDueDateAnchor] = useState<HTMLElement | null>(null)
  const [estimatedHoursAnchor, setEstimatedHoursAnchor] = useState<HTMLElement | null>(null)
  const [conflictError, setConflictError] = useState<string | null>(null)
  const [detailsError, setDetailsError] = useState<string | null>(null)
  const [isAddingSubtask, setIsAddingSubtask] = useState(false)
  const [newSubtaskDescription, setNewSubtaskDescription] = useState('')
  const [pendingSubtasks, setPendingSubtasks] = useState<{ description: string; completed: boolean }[]>([])
  const [deletedSubtaskIds, setDeletedSubtaskIds] = useState<Set<Guid>>(new Set())
  const [toggledSubtasks, setToggledSubtasks] = useState<Map<Guid, boolean>>(new Map())
  const [reorderedSubtaskIds, setReorderedSubtaskIds] = useState<Guid[] | null>(null)
  const newSubtaskInputRef = useRef<HTMLInputElement | null>(null)
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const [columnIdDraft, setColumnIdDraft] = useState<Guid>(card.columnId)
  const [isDragOver, setIsDragOver] = useState(false)
  const [attachmentError, setAttachmentError] = useState<string | null>(null)
  const [uploadingFileName, setUploadingFileName] = useState<string | null>(null)
  const [newCommentContent, setNewCommentContent] = useState('')
  const [editingCommentId, setEditingCommentId] = useState<Guid | null>(null)
  const [editingCommentContent, setEditingCommentContent] = useState('')
  const [activityShowCount, setActivityShowCount] = useState(5)
  const descriptionDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const [tagMenuAnchor, setTagMenuAnchor] = useState<HTMLElement | null>(null)
  const [assigneeMenuAnchor, setAssigneeMenuAnchor] = useState<HTMLElement | null>(null)


  const dndSensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  // Auto-save description with 1.5s debounce
  const handleDescriptionChange = useCallback((value: string | undefined) => {
    const next = value ?? ''
    setDescriptionDraft(next)
    if (descriptionDebounceRef.current) {
      clearTimeout(descriptionDebounceRef.current)
    }
  }, [])

  useEffect(() => {
    const ref = descriptionDebounceRef
    return () => {
      if (ref.current) {
        clearTimeout(ref.current)
      }
    }
  }, [])

  const sortedMembers = useMemo(
    () =>
      [...members].sort((a, b) => {
        const left = getMemberLabel(a)
        const right = getMemberLabel(b)
        return left.localeCompare(right)
      }),
    [members],
  )

  const currentTagIds = useMemo(() => new Set((card.cardTags ?? []).map((cardTag) => cardTag.tagId)), [card.cardTags])
  const currentAssigneeIds = useMemo(
    () => new Set((card.assignments ?? []).map((assignment) => assignment.userId)),
    [card.assignments],
  )

  const isSavingCore = updateCardMutation.isPending
  const isSavingTags = assignTagMutation.isPending || unassignTagMutation.isPending
  const isSavingAssignees = assignUserMutation.isPending || unassignUserMutation.isPending
  const isSavingSubtasks = createSubtaskMutation.isPending || updateSubtaskMutation.isPending || deleteSubtaskMutation.isPending
  const isArchiving = archiveCardMutation.isPending
  const isUploadingAttachment = addAttachmentMutation.isPending
  const isDeletingAttachment = removeAttachmentMutation.isPending
  const isSavingComment = createCommentMutation.isPending || updateCommentMutation.isPending || deleteCommentMutation.isPending

  const isBusy = isSavingCore || isSavingTags || isSavingAssignees || isSavingSubtasks || isArchiving || isUploadingAttachment || isDeletingAttachment

  const currentUserId = currentUser?.id
  const isProjectManager = currentUserRole !== undefined && currentUserRole <= 1
  const isCardCreator = currentUserId !== undefined && card.createdBy === currentUserId
  const canDeleteAttachment = (attachment: Attachment) =>
    canManageCards && (
      attachment.uploadedBy === currentUserId ||
      isCardCreator ||
      isProjectManager
    )

  const sortedSubtasks = useMemo(() => {
    const filtered = [...(card.subtasks ?? [])]
      .filter((subtask) => !deletedSubtaskIds.has(subtask.id))
      .map((subtask) => toggledSubtasks.has(subtask.id) ? { ...subtask, completed: toggledSubtasks.get(subtask.id)! } : subtask)
    if (reorderedSubtaskIds) {
      const idMap = new Map(filtered.map((s) => [s.id, s]))
      return reorderedSubtaskIds.filter((id) => idMap.has(id)).map((id) => idMap.get(id)!)
    }
    return filtered.sort((left, right) => left.position - right.position)
  }, [card.subtasks, deletedSubtaskIds, toggledSubtasks, reorderedSubtaskIds])

  const completedSubtaskCount = useMemo(
    () => sortedSubtasks.filter((subtask) => subtask.completed).length + pendingSubtasks.filter((p) => p.completed).length,
    [sortedSubtasks, pendingSubtasks],
  )

  const totalSubtaskCount = sortedSubtasks.length + pendingSubtasks.length

  const dateError = startDateDraft && dueDateDraft && dueDateDraft < startDateDraft
    ? 'Due date cannot be before start date'
    : null

  const currentEstimatedHoursDraft = card.estimatedHours != null ? String(card.estimatedHours) : ''
  const parsedEstimatedHours = estimatedHoursDraft.trim().length > 0 ? Number(estimatedHoursDraft) : null
  const estimatedHoursError =
    estimatedHoursDraft.trim().length > 0 && (Number.isNaN(parsedEstimatedHours) || (parsedEstimatedHours ?? 0) < 0)
      ? 'Estimated hours must be a non-negative number'
      : null

  const hasCoreChanges =
    titleDraft.trim().length > 0 &&
    (titleDraft.trim() !== card.title ||
      descriptionDraft !== (card.description ?? '') ||
      startDateDraft !== (card.startDate ? formatDateShort(card.startDate) : '') ||
      dueDateDraft !== (card.dueDate ? formatDateShort(card.dueDate) : '') ||
      estimatedHoursDraft !== currentEstimatedHoursDraft)

  const hasTagChanges =
    selectedTagIds.length !== currentTagIds.size || selectedTagIds.some((tagId) => !currentTagIds.has(tagId))

  const hasAssigneeChanges =
    selectedAssigneeIds.length !== currentAssigneeIds.size || selectedAssigneeIds.some((userId) => !currentAssigneeIds.has(userId))

  const hasSubtaskChanges = pendingSubtasks.length > 0 || deletedSubtaskIds.size > 0 || toggledSubtasks.size > 0 || reorderedSubtaskIds !== null
  const hasColumnChange = columnIdDraft !== card.columnId

  const hasAnyChanges = hasCoreChanges || hasTagChanges || hasAssigneeChanges || hasSubtaskChanges || hasColumnChange
  const canSaveChanges = canManageCards && titleDraft.trim().length > 0 && !dateError && !estimatedHoursError

  const availableTagsForMenu = useMemo(
    () => tags.filter((tag) => !selectedTagIds.includes(tag.id)),
    [tags, selectedTagIds],
  )

  const availableMembersForMenu = useMemo(
    () => sortedMembers.filter((member) => !selectedAssigneeIds.includes(member.userId)),
    [sortedMembers, selectedAssigneeIds],
  )

  const handleClose = () => {
    if (isBusy) {
      return
    }

    onClose()
  }

  const handleSaveChanges = async () => {
    if (!canSaveChanges) {
      if (!hasAnyChanges) {
        onClose()
        return
      }
      return
    }

    setConflictError(null)
    setDetailsError(null)

    try {
      if (hasCoreChanges) {
        await updateCardMutation.mutateAsync({
          id: card.id,
          data: {
            title: titleDraft.trim(),
            description: descriptionDraft.trim().length > 0 ? descriptionDraft : null,
            startDate: startDateDraft.trim().length > 0 ? new Date(startDateDraft).toISOString() : null,
            dueDate: dueDateDraft.trim().length > 0 ? new Date(dueDateDraft).toISOString() : null,
            estimatedHours: parsedEstimatedHours,
            version: card.version,
          },
        })
      }

      if (hasTagChanges) {
        const desired = new Set(selectedTagIds)
        const current = new Set((card.cardTags ?? []).map((cardTag) => cardTag.tagId))

        const toAdd = selectedTagIds.filter((tagId) => !current.has(tagId))
        const toRemove = [...current].filter((tagId) => !desired.has(tagId))

        for (const tagId of toAdd) {
          await assignTagMutation.mutateAsync({ cardId: card.id, tagId })
        }

        for (const tagId of toRemove) {
          await unassignTagMutation.mutateAsync({ cardId: card.id, tagId })
        }
      }

      if (hasAssigneeChanges) {
        const desired = new Set(selectedAssigneeIds)
        const current = new Set((card.assignments ?? []).map((assignment) => assignment.userId))

        const toAdd = selectedAssigneeIds.filter((userId) => !current.has(userId))
        const toRemove = [...current].filter((userId) => !desired.has(userId))

        for (const userId of toAdd) {
          await assignUserMutation.mutateAsync({ cardId: card.id, userId })
        }

        for (const userId of toRemove) {
          await unassignUserMutation.mutateAsync({ cardId: card.id, userId })
        }
      }

      if (hasColumnChange) {
        await moveCardMutation.mutateAsync({
          id: card.id,
          boardId: card.column?.boardId ?? columns[0]?.boardId ?? '',
          data: { columnId: columnIdDraft, position: card.position },
        })
      }

      if (hasSubtaskChanges) {
        if (reorderedSubtaskIds) {
          for (let i = 0; i < reorderedSubtaskIds.length; i++) {
            const subtaskId = reorderedSubtaskIds[i]
            if (deletedSubtaskIds.has(subtaskId)) continue
            const original = (card.subtasks ?? []).find((s) => s.id === subtaskId)
            const newPosition = (i + 1) * 1000
            if (original && original.position !== newPosition) {
              await updateSubtaskMutation.mutateAsync({
                id: subtaskId,
                cardId: card.id,
                data: { position: newPosition },
              })
            }
          }
        }

        for (const [subtaskId, completed] of toggledSubtasks) {
          await updateSubtaskMutation.mutateAsync({
            id: subtaskId,
            cardId: card.id,
            data: { completed },
          })
        }

        for (const subtaskId of deletedSubtaskIds) {
          await deleteSubtaskMutation.mutateAsync({
            id: subtaskId,
            cardId: card.id,
          })
        }

        for (const pending of pendingSubtasks) {
          await createSubtaskMutation.mutateAsync({
            cardId: card.id,
            data: { description: pending.description, completed: pending.completed || undefined },
          })
        }

        setToggledSubtasks(new Map())
        setDeletedSubtaskIds(new Set())
        setPendingSubtasks([])
        setReorderedSubtaskIds(null)
      }

      onClose()
    } catch (error) {
      const axiosError = error as AxiosError
      if (axiosError.response?.status === 409) {
        setConflictError('This task was updated elsewhere. Refresh to load the latest version, then re-apply your edits.')
        return
      }

      setDetailsError('Unable to save changes. Please try again.')
    }
  }

  const handleRefresh = async () => {
    setConflictError(null)
    setDetailsError(null)
    await onRefresh()
  }

  const handleArchive = async () => {
    if (!canManageCards) {
      return
    }

    setDetailsError(null)

    try {
      await archiveCardMutation.mutateAsync(card.id)
      onClose()
    } catch {
      setDetailsError('Unable to archive this task. Please try again.')
    }
  }

  const handleColumnChange = (columnId: Guid) => {
    if (columnId === card.columnId || !canManageCards) return
    setColumnIdDraft(columnId)
  }

  const handleSubtaskDragEnd = (event: DragEndEvent) => {
    const { active, over } = event
    if (!over || active.id === over.id) return
    const ids = reorderedSubtaskIds ?? sortedSubtasks.map((s) => s.id)
    const oldIndex = ids.indexOf(active.id as Guid)
    const newIndex = ids.indexOf(over.id as Guid)
    if (oldIndex === -1 || newIndex === -1) return
    const newIds = [...ids]
    newIds.splice(oldIndex, 1)
    newIds.splice(newIndex, 0, active.id as Guid)
    setReorderedSubtaskIds(newIds)
  }

  const saveSubtaskDescription = async (subtaskId: Guid, descriptionValue: string) => {
    const subtask = sortedSubtasks.find((item) => item.id === subtaskId)
    if (!subtask) {
      return
    }

    const nextDescription = descriptionValue.trim()
    if (nextDescription.length === 0 || nextDescription === subtask.description) {
      return
    }

    setDetailsError(null)

    try {
      await updateSubtaskMutation.mutateAsync({
        id: subtaskId,
        cardId: card.id,
        data: {
          description: nextDescription,
        },
      })
    } catch {
      setDetailsError('Unable to update subtasks. Please try again.')
    }
  }

  const handleToggleSubtask = (subtaskId: Guid, completed: boolean) => {
    const original = (card.subtasks ?? []).find((s) => s.id === subtaskId)
    if (original && original.completed === completed) {
      setToggledSubtasks((prev) => {
        const next = new Map(prev)
        next.delete(subtaskId)
        return next
      })
    } else {
      setToggledSubtasks((prev) => new Map(prev).set(subtaskId, completed))
    }
  }

  const handleDeleteSubtask = (subtaskId: Guid) => {
    setDeletedSubtaskIds((prev) => new Set(prev).add(subtaskId))
    setToggledSubtasks((prev) => {
      const next = new Map(prev)
      next.delete(subtaskId)
      return next
    })
  }

  const focusNewSubtaskInput = () => {
    requestAnimationFrame(() => {
      newSubtaskInputRef.current?.focus()
    })
  }

  const handleStartAddingSubtask = () => {
    if (!canManageCards || isBusy) {
      return
    }

    setIsAddingSubtask(true)
    focusNewSubtaskInput()
  }

  const handleAddPendingSubtask = () => {
    if (!canManageCards || isBusy) {
      return
    }

    const description = newSubtaskDescription.trim()
    if (description.length === 0) {
      focusNewSubtaskInput()
      return
    }

    setPendingSubtasks((prev) => [...prev, { description, completed: false }])
    setNewSubtaskDescription('')
    setIsAddingSubtask(true)
    focusNewSubtaskInput()
  }

  const handleRemoveTag = (tagId: Guid) => {
    setSelectedTagIds((prev) => prev.filter((id) => id !== tagId))
  }

  const handleAddTag = (tagId: Guid) => {
    setSelectedTagIds((prev) => [...prev, tagId])
  }

  const handleRemoveAssignee = (userId: Guid) => {
    setSelectedAssigneeIds((prev) => prev.filter((id) => id !== userId))
  }

  const handleAddAssignee = (userId: Guid) => {
    setSelectedAssigneeIds((prev) => [...prev, userId])
  }

  const handleUploadFile = useCallback(async (file: File) => {
    setAttachmentError(null)
    const existingTotalBytes = (card.attachments ?? []).reduce((sum, a) => sum + a.fileSize, 0)
    const validationError = validateFile(file, existingTotalBytes)
    if (validationError) {
      setAttachmentError(validationError)
      return
    }
    setUploadingFileName(file.name)
    try {
      await addAttachmentMutation.mutateAsync({ cardId: card.id, file })
    } catch (error) {
      const axiosError = error as AxiosError<{ message?: string }>
      setAttachmentError(axiosError.response?.data?.message ?? 'Failed to upload attachment.')
    } finally {
      setUploadingFileName(null)
    }
  }, [card.id, card.attachments, addAttachmentMutation])

  const handleFileInputChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (file) {
      void handleUploadFile(file)
    }
    if (event.target) {
      event.target.value = ''
    }
  }

  const handleDragOver = (event: DragEvent) => {
    event.preventDefault()
    setIsDragOver(true)
  }

  const handleDragLeave = (event: DragEvent) => {
    event.preventDefault()
    setIsDragOver(false)
  }

  const handleDrop = (event: DragEvent) => {
    event.preventDefault()
    setIsDragOver(false)
    const file = event.dataTransfer.files[0]
    if (file) {
      void handleUploadFile(file)
    }
  }

  const handleDeleteAttachment = async (attachment: Attachment) => {
    setAttachmentError(null)
    try {
      await removeAttachmentMutation.mutateAsync({ id: attachment.id, cardId: card.id })
    } catch {
      setAttachmentError('Failed to delete attachment.')
    }
  }

  const handleDownloadAttachment = async (attachment: Attachment) => {
    try {
      await downloadAttachment(attachment.id, attachment.filename)
    } catch {
      setAttachmentError('Failed to download file.')
    }
  }

  const activityItems = useMemo(() => {
    return (activitiesQuery.data ?? []).map((a) => ({
      id: a.id,
      date: a.createdAt,
      actor: a.user?.userName ?? a.user?.email ?? 'Someone',
      description: formatActivityDescription(a.action, a.field, a.oldValue, a.newValue),
    }))
  }, [activitiesQuery.data])

  const visibleActivities = useMemo(
    () => activityItems.slice(0, activityShowCount),
    [activityItems, activityShowCount],
  )
  const activityRemaining = activityItems.length - activityShowCount
  const activityHasMore = activityRemaining > 0

  const activityByDate = useMemo(() => {
    const groups = new Map<string, typeof activityItems>()
    for (const item of visibleActivities) {
      const dateKey = formatDateShort(item.date)
      const existing = groups.get(dateKey)
      if (existing) {
        existing.push(item)
      } else {
        groups.set(dateKey, [item])
      }
    }
    return groups
  }, [visibleActivities])

  const currentColumn = columns.find((col) => col.id === columnIdDraft)

  return (
    <>
      {/* Header bar */}
      <Box sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 1.5,
        px: 3,
        pt: 2,
        pb: 1.5,
        borderBottom: '1px solid',
        borderColor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.12)' : 'rgba(13, 148, 136, 0.1)',
        bgcolor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.04)' : 'rgba(13, 148, 136, 0.03)',
      }}>
        {currentColumn ? (
          <Chip
            icon={<FiberManualRecordIcon sx={{ fontSize: 10 }} />}
            label={currentColumn.name}
            size="small"
            color="primary"
            variant="outlined"
            sx={{ fontWeight: 600, fontSize: '0.75rem' }}
          />
        ) : null}
        <Box sx={{ flex: 1 }} />
        <SubscribeButton entityType={0} entityId={card.id} disabled={isBusy} />
        <Tooltip title="Close (Esc)">
          <IconButton size="small" onClick={handleClose} disabled={isBusy} aria-label="Close dialog">
            <CloseIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Box>

      <DialogContent sx={{ px: 3, pt: 1.5, pb: 3 }}>
        <Box sx={{ display: 'flex', gap: 4, flexDirection: { xs: 'column', md: 'row' } }}>
          {/* Left column (main content) */}
          <Box sx={{ flex: '1 1 0%', minWidth: 0 }}>
            <Stack spacing={3}>
              {/* Title */}
              <TextField
                value={titleDraft}
                onChange={(event) => setTitleDraft(event.target.value)}
                disabled={!canManageCards || isBusy}
                fullWidth
                variant="standard"
                placeholder="Task title"
                slotProps={{
                  input: {
                    disableUnderline: true,
                    sx: {
                      fontSize: '1.625rem',
                      fontWeight: 700,
                      lineHeight: 1.3,
                      letterSpacing: '-0.01em',
                    },
                  },
                }}
              />

              {/* Details / Activity tabs */}
              <Tabs
                value={activeTab}
                onChange={(_, val) => setActiveTab(val)}
                sx={{
                  minHeight: 36,
                  borderBottom: '1px solid',
                  borderColor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.12)' : 'rgba(13, 148, 136, 0.08)',
                  '& .MuiTab-root': {
                    minHeight: 36,
                    py: 0.5,
                    textTransform: 'none',
                    fontWeight: 600,
                    fontSize: '0.8125rem',
                  },
                }}
              >
                <Tab label="Details" />
                <Tab label="Activity" icon={<HistoryIcon sx={{ fontSize: 16 }} />} iconPosition="start" />
              </Tabs>

              {activeTab === 0 ? (
                <Stack spacing={3}>
                  {/* Description (plain text with auto-linked URLs) */}
                  <Stack spacing={0.75}>
                    <Stack direction="row" alignItems="center" spacing={1}>
                      <SubjectIcon sx={{ color: 'primary.main', fontSize: 20 }} />
                      <Typography variant="subtitle2" fontWeight={700}>Description</Typography>
                    </Stack>
                    {descriptionMode === 'edit' && canManageCards ? (
                      <DescriptionEditor
                        value={descriptionDraft}
                        onChange={handleDescriptionChange}
                        onBlur={() => setDescriptionMode('preview')}
                      />
                    ) : (
                      <Box
                        onClick={(e) => {
                          if ((e.target as HTMLElement).closest('a')) return
                          if (canManageCards) setDescriptionMode('edit')
                        }}
                        sx={{
                          minHeight: 48,
                          border: '1px solid',
                          borderColor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.15)' : 'rgba(13, 148, 136, 0.1)',
                          borderRadius: 1.5,
                          p: 1.5,
                          cursor: canManageCards ? 'pointer' : 'default',
                          '&:hover': canManageCards ? { borderColor: 'primary.main', bgcolor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.04)' : 'rgba(13, 148, 136, 0.02)' } : {},
                        }}
                      >
                        {descriptionDraft.trim().length > 0 ? (
                          <Typography variant="body2" component="div" sx={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
                            {linkifyText(descriptionDraft)}
                          </Typography>
                        ) : (
                          <Typography variant="body2" color="text.disabled" sx={{ fontStyle: 'italic' }}>
                            Add a description...
                          </Typography>
                        )}
                      </Box>
                    )}
                  </Stack>

                  {/* Subtasks (drag to reorder) */}
                  <Stack spacing={1.5}>
                    <Stack direction="row" alignItems="center" spacing={1}>
                      <CheckCircleIcon sx={{ color: 'primary.main', fontSize: 20 }} />
                      <Typography variant="subtitle2" fontWeight={700}>Subtasks</Typography>
                      {totalSubtaskCount > 0 ? (
                        <Chip
                          label={`${completedSubtaskCount}/${totalSubtaskCount}`}
                          size="small"
                          color={completedSubtaskCount === totalSubtaskCount ? 'success' : 'default'}
                          variant="outlined"
                          sx={{ height: 20, fontSize: '0.6875rem', ml: 'auto' }}
                        />
                      ) : null}
                    </Stack>

                    {totalSubtaskCount > 0 ? (
                      <Stack spacing={0.75}>
                        <LinearProgress
                          variant="determinate"
                          value={(completedSubtaskCount / totalSubtaskCount) * 100}
                          color={completedSubtaskCount === totalSubtaskCount ? 'success' : 'primary'}
                          sx={{
                            height: 4,
                            borderRadius: 2,
                            bgcolor: 'divider',
                            '& .MuiLinearProgress-bar': { borderRadius: 2 },
                          }}
                        />
                        <DndContext
                          sensors={dndSensors}
                          collisionDetection={closestCenter}
                          onDragEnd={handleSubtaskDragEnd}
                        >
                          <SortableContext
                            items={sortedSubtasks.map((s) => s.id)}
                            strategy={verticalListSortingStrategy}
                          >
                            {sortedSubtasks.map((subtask) => (
                              <SortableSubtaskItem
                                key={subtask.id}
                                subtask={subtask}
                                canManageCards={canManageCards}
                                isBusy={isBusy}
                                onToggle={handleToggleSubtask}
                                onDelete={handleDeleteSubtask}
                                onSaveDescription={saveSubtaskDescription}
                              />
                            ))}
                          </SortableContext>
                        </DndContext>
                      </Stack>
                    ) : (
                      <Typography variant="body2" color="text.secondary" sx={{ fontStyle: 'italic' }}>
                        No subtasks yet.
                      </Typography>
                    )}

                    {pendingSubtasks.length > 0 ? (
                      <Stack spacing={1}>
                        {pendingSubtasks.map((pending, index) => (
                          <Box
                            key={index}
                            sx={{
                              display: 'flex',
                              alignItems: 'center',
                              gap: 1,
                              bgcolor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.04)' : 'rgba(13, 148, 136, 0.03)',
                              borderRadius: 1,
                              px: 1,
                              py: 0.5,
                              border: '1px dashed',
                              borderColor: 'primary.main',
                            }}
                          >
                            <Checkbox
                              checked={pending.completed}
                              onChange={() => setPendingSubtasks((prev) => prev.map((item, i) => i === index ? { ...item, completed: !item.completed } : item))}
                              disabled={isBusy}
                              sx={{ color: 'primary.main' }}
                            />
                            <TextField
                              variant="standard"
                              size="small"
                              value={pending.description}
                              onChange={(event) => setPendingSubtasks((prev) => prev.map((item, i) => i === index ? { ...item, description: event.target.value } : item))}
                              disabled={isBusy}
                              fullWidth
                              slotProps={{
                                input: {
                                  disableUnderline: true,
                                  sx: {
                                    fontSize: '0.875rem',
                                    textDecoration: pending.completed ? 'line-through' : 'none',
                                    opacity: pending.completed ? 0.6 : 1,
                                  },
                                },
                              }}
                            />
                            <IconButton
                              color="error"
                              size="small"
                              onClick={() => setPendingSubtasks((prev) => prev.filter((_, i) => i !== index))}
                              disabled={isBusy}
                              aria-label="Remove pending subtask"
                            >
                              <DeleteOutlineIcon fontSize="small" />
                            </IconButton>
                          </Box>
                        ))}
                      </Stack>
                    ) : null}

                    {canManageCards ? (
                      <Stack direction="row" spacing={1} alignItems="center">
                        {isAddingSubtask ? (
                          <TextField
                            inputRef={newSubtaskInputRef}
                            size="small"
                            placeholder="New subtask"
                            value={newSubtaskDescription}
                            onChange={(event) => setNewSubtaskDescription(event.target.value)}
                            onKeyDown={(event) => {
                              if (event.key === 'Enter') {
                                event.preventDefault()
                                handleAddPendingSubtask()
                              }
                              if (event.key === 'Escape') {
                                setNewSubtaskDescription('')
                                setIsAddingSubtask(false)
                              }
                            }}
                            disabled={isBusy}
                            fullWidth
                          />
                        ) : null}
                        <Button
                          variant="text"
                          color="primary"
                          onClick={isAddingSubtask ? handleAddPendingSubtask : handleStartAddingSubtask}
                          disabled={isBusy}
                          sx={{ whiteSpace: 'nowrap' }}
                        >
                          {isAddingSubtask ? 'Add' : '+ Add an item'}
                        </Button>
                      </Stack>
                    ) : null}
                  </Stack>

                  {/* Comments */}
                  <Stack spacing={2}>
                    <Stack direction="row" alignItems="center" spacing={1}>
                      <ChatBubbleOutlineIcon sx={{ color: 'primary.main', fontSize: 20 }} />
                      <Typography variant="subtitle2" fontWeight={700}>Comments</Typography>
                      {(commentsQuery.data ?? []).length > 0 ? (
                        <Chip label={`${(commentsQuery.data ?? []).length}`} size="small" variant="outlined" sx={{ height: 20, fontSize: '0.6875rem' }} />
                      ) : null}
                    </Stack>

                    {commentsQuery.isLoading ? (
                      <Box sx={{ display: 'flex', justifyContent: 'center', py: 2 }}>
                        <CircularProgress size={24} />
                      </Box>
                    ) : null}

                    {(commentsQuery.data ?? []).length > 0 ? (
                      <Stack spacing={1}>
                        {(commentsQuery.data ?? []).map((comment) => (
                          <Box
                            key={comment.id}
                            sx={{
                              borderLeft: '3px solid',
                              borderColor: currentUser?.id === comment.authorId ? 'primary.main' : (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.2)' : 'rgba(13, 148, 136, 0.15)',
                              borderRadius: '0 8px 8px 0',
                              bgcolor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.04)' : 'rgba(13, 148, 136, 0.03)',
                              pl: 1.5,
                              pr: 1,
                              py: 1,
                              '&:hover .comment-actions': { opacity: 1 },
                            }}
                          >
                            <Stack direction="row" alignItems="center" spacing={0.75} sx={{ mb: 0.5 }}>
                              <UserAvatar
                                userId={comment.authorId}
                                name={comment.author?.userName ?? comment.author?.email}
                                sx={{ width: 22, height: 22, fontSize: '0.65rem', bgcolor: 'primary.main' }}
                              />
                              <Typography variant="caption" fontWeight={600}>
                                {comment.author?.userName ?? comment.author?.email ?? 'Unknown'}
                              </Typography>
                              <Typography variant="caption" color="text.disabled" sx={{ fontSize: '0.6875rem' }}>
                                {formatDateTime(comment.createdAt)}
                                {comment.updatedAt !== comment.createdAt ? ' (edited)' : ''}
                              </Typography>
                              <Box sx={{ flex: 1 }} />
                              {currentUser?.id === comment.authorId ? (
                                <Stack direction="row" spacing={0.25} className="comment-actions" sx={{ opacity: 0 }}>
                                  <IconButton
                                    size="small"
                                    onClick={() => {
                                      setEditingCommentId(comment.id)
                                      setEditingCommentContent(comment.content)
                                    }}
                                    disabled={isSavingComment}
                                    aria-label="Edit comment"
                                    sx={{ p: 0.25 }}
                                  >
                                    <EditIcon sx={{ fontSize: 14 }} />
                                  </IconButton>
                                  <IconButton
                                    size="small"
                                    color="error"
                                    onClick={() => {
                                      deleteCommentMutation.mutate({ id: comment.id, cardId: card.id })
                                    }}
                                    disabled={isSavingComment}
                                    aria-label="Delete comment"
                                    sx={{ p: 0.25 }}
                                  >
                                    <DeleteOutlineIcon sx={{ fontSize: 14 }} />
                                  </IconButton>
                                </Stack>
                              ) : null}
                            </Stack>
                            {editingCommentId === comment.id ? (
                              <Stack direction="row" spacing={1} alignItems="flex-start">
                                <TextField
                                  value={editingCommentContent}
                                  onChange={(e) => setEditingCommentContent(e.target.value)}
                                  multiline
                                  minRows={1}
                                  maxRows={6}
                                  size="small"
                                  fullWidth
                                  disabled={isSavingComment}
                                />
                                <Button
                                  variant="contained"
                                  size="small"
                                  disabled={!editingCommentContent.trim() || isSavingComment}
                                  onClick={() => {
                                    updateCommentMutation.mutate(
                                      { id: comment.id, content: editingCommentContent.trim(), cardId: card.id },
                                      {
                                        onSuccess: () => {
                                          setEditingCommentId(null)
                                          setEditingCommentContent('')
                                        },
                                      },
                                    )
                                  }}
                                >
                                  Save
                                </Button>
                                <Button
                                  size="small"
                                  onClick={() => {
                                    setEditingCommentId(null)
                                    setEditingCommentContent('')
                                  }}
                                >
                                  Cancel
                                </Button>
                              </Stack>
                            ) : (
                              <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word', pl: 3.5 }}>
                                {comment.content}
                              </Typography>
                            )}
                          </Box>
                        ))}
                      </Stack>
                    ) : !commentsQuery.isLoading ? (
                      <Typography variant="body2" color="text.disabled" sx={{ fontStyle: 'italic' }}>
                        No comments yet.
                      </Typography>
                    ) : null}

                    {/* New comment input */}
                    <Stack direction="row" spacing={1} alignItems="center">
                      <UserAvatar
                        userId={currentUser?.id}
                        name={currentUser?.userName ?? currentUser?.email}
                        sx={{ width: 26, height: 26, fontSize: '0.7rem', bgcolor: 'primary.main' }}
                      />
                      <TextField
                        value={newCommentContent}
                        onChange={(e) => setNewCommentContent(e.target.value)}
                        placeholder="Write a comment..."
                        multiline
                        minRows={1}
                        maxRows={4}
                        size="small"
                        fullWidth
                        disabled={isSavingComment}
                      />
                      <IconButton
                        color="primary"
                        size="small"
                        onClick={() => {
                          if (!newCommentContent.trim()) return
                          createCommentMutation.mutate(
                            { cardId: card.id, content: newCommentContent.trim() },
                            { onSuccess: () => setNewCommentContent('') },
                          )
                        }}
                        disabled={!newCommentContent.trim() || isSavingComment}
                        aria-label="Post comment"
                        sx={{ mt: 0.5 }}
                      >
                        <SendIcon sx={{ fontSize: 18 }} />
                      </IconButton>
                    </Stack>
                  </Stack>
                </Stack>
              ) : (
                /* Activity tab */
                <Stack spacing={2}>
                  {activitiesQuery.isLoading ? (
                    <Typography variant="body2" color="text.secondary">Loading activity...</Typography>
                  ) : activityByDate.size > 0 ? (
                    <>
                      {[...activityByDate.entries()].map(([dateKey, items]) => (
                        <Stack key={dateKey} spacing={1}>
                          <Typography variant="caption" fontWeight={600} color="text.secondary">
                            {dateKey}
                          </Typography>
                          {items.map((item) => (
                            <Stack key={item.id} direction="row" spacing={1.5} alignItems="center" sx={{ pl: 1 }}>
                              <Box sx={{ width: 6, height: 6, borderRadius: '50%', bgcolor: 'primary.main', flexShrink: 0 }} />
                              <Typography variant="body2">
                                <Typography component="span" variant="body2" fontWeight={600}>{item.actor}</Typography>
                                {' '}{item.description}
                              </Typography>
                              <Typography variant="caption" color="text.secondary" sx={{ ml: 'auto', whiteSpace: 'nowrap' }}>
                                {formatDateTime(item.date)}
                              </Typography>
                            </Stack>
                          ))}
                        </Stack>
                      ))}
                      {(activityHasMore || activityShowCount > 5) && (
                        <Stack direction="row" spacing={1}>
                          {activityHasMore && (
                            <Button
                              size="small"
                              onClick={() => setActivityShowCount((v) => v + 5)}
                              endIcon={<ExpandMoreIcon />}
                              sx={{ color: 'text.secondary' }}
                            >
                              {activityRemaining <= 5
                                ? `Show ${activityRemaining} more`
                                : `Show 5 more (${activityRemaining - 5} others)`}
                            </Button>
                          )}
                          {activityShowCount > 5 && (
                            <Button
                              size="small"
                              onClick={() => setActivityShowCount(5)}
                              endIcon={<ExpandLessIcon />}
                              sx={{ color: 'text.secondary' }}
                            >
                              Show less
                            </Button>
                          )}
                        </Stack>
                      )}
                    </>
                  ) : (
                    <Typography variant="body2" color="text.secondary">No activity yet.</Typography>
                  )}
                </Stack>
              )}

              {/* Alerts */}
              {conflictError ? (
                <Alert
                  severity="warning"
                  action={
                    <Button color="inherit" size="small" onClick={handleRefresh}>
                      Refresh
                    </Button>
                  }
                >
                  {conflictError}
                </Alert>
              ) : null}

              {detailsError ? <Alert severity="error">{detailsError}</Alert> : null}
            </Stack>
          </Box>

          {/* Right sidebar */}
          <Box sx={{
            width: { xs: '100%', md: '300px' },
            maxWidth: { xs: '100%', md: '300px' },
            flex: { md: '0 0 300px' },
            flexShrink: 0,
            alignSelf: 'flex-start',
            overflow: 'hidden',
          }}>
            <Stack spacing={0}>
              {/* Properties section */}
              <Box sx={{
                bgcolor: 'background.paper',
                borderRadius: 2,
                border: '1px solid',
                borderColor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.15)' : 'rgba(13, 148, 136, 0.12)',
                overflow: 'hidden',
              }}>
                <Typography variant="caption" fontWeight={700} color="primary.main" sx={{ display: 'block', px: 2, pt: 1.5, pb: 1, letterSpacing: '0.05em', textTransform: 'uppercase', fontSize: '0.6875rem' }}>
                  Properties
                </Typography>

                {/* Column selector */}
                {columns.length > 0 ? (
                  <Stack direction="row" alignItems="center" sx={{ px: 2, py: 1, minWidth: 0, '&:hover': { bgcolor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.06)' : 'rgba(13, 148, 136, 0.04)' } }}>
                    <Typography variant="body2" color="text.secondary" sx={{ width: 80, flexShrink: 0, fontSize: '0.8125rem' }}>List</Typography>
                    <Select
                      size="small"
                      value={columnIdDraft}
                      onChange={(e) => handleColumnChange(e.target.value as Guid)}
                      disabled={!canManageCards || isBusy}
                      fullWidth
                      sx={{
                        minWidth: 0,
                        '& .MuiSelect-select': {
                          py: 0.5,
                          fontSize: '0.8125rem',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          whiteSpace: 'nowrap',
                        },
                      }}
                    >
                      {columns.map((col) => (
                        <MenuItem key={col.id} value={col.id}>{col.name}</MenuItem>
                      ))}
                    </Select>
                  </Stack>
                ) : null}

                {/* Assignees */}
                <Divider />
                <Stack sx={{ px: 2, py: 1.5 }}>
                      <Stack direction="row" alignItems="center" sx={{ mb: selectedAssigneeIds.length > 0 ? 1 : 0 }}>
                        <Typography variant="body2" color="text.secondary" sx={{ width: 80, flexShrink: 0, fontSize: '0.8125rem' }}>Assignees</Typography>
                        {canManageCards && availableMembersForMenu.length > 0 ? (
                          <IconButton
                            size="small"
                            onClick={(event: MouseEvent<HTMLButtonElement>) => setAssigneeMenuAnchor(event.currentTarget)}
                            disabled={isBusy}
                            aria-label="Add assignee"
                            sx={{ ml: 'auto', width: 28, height: 28, border: '1px dashed', borderColor: 'primary.main', color: 'primary.main', opacity: 0.6, '&:hover': { opacity: 1 } }}
                          >
                            <AddIcon sx={{ fontSize: 16 }} />
                          </IconButton>
                        ) : null}
                      </Stack>
                      {selectedAssigneeIds.length > 0 ? (
                        <Stack spacing={0.75}>
                          {selectedAssigneeIds.map((userId) => {
                            const member = sortedMembers.find((item) => item.userId === userId)
                            const label = member ? getMemberLabel(member) : userId
                            return (
                              <Chip
                                key={userId}
                                avatar={<UserAvatar userId={userId} name={label} sx={{ width: 22, height: 22, fontSize: '0.7rem' }} />}
                                label={label}
                                onDelete={canManageCards && !isBusy ? () => handleRemoveAssignee(userId) : undefined}
                                size="small"
                                sx={{ justifyContent: 'flex-start', width: '100%', '& .MuiChip-label': { flex: 1 } }}
                              />
                            )
                          })}
                        </Stack>
                      ) : null}
                      <Menu
                        anchorEl={assigneeMenuAnchor}
                        open={Boolean(assigneeMenuAnchor)}
                        onClose={() => setAssigneeMenuAnchor(null)}
                      >
                        {availableMembersForMenu.map((member) => (
                          <MenuItem key={member.userId} onClick={() => handleAddAssignee(member.userId)}>
                            {getMemberLabel(member)}
                          </MenuItem>
                        ))}
                      </Menu>
                    </Stack>

                {/* Tags */}
                <Divider />
                <Stack sx={{ px: 2, py: 1.5 }}>
                  <Stack direction="row" alignItems="center" sx={{ mb: selectedTagIds.length > 0 ? 1 : 0 }}>
                    <Typography variant="body2" color="text.secondary" sx={{ width: 80, flexShrink: 0, fontSize: '0.8125rem' }}>Tags</Typography>
                    {canManageCards && availableTagsForMenu.length > 0 && selectedTagIds.length < 3 ? (
                      <IconButton
                        size="small"
                        onClick={(event: MouseEvent<HTMLButtonElement>) => setTagMenuAnchor(event.currentTarget)}
                        disabled={isBusy}
                        aria-label="Add tag"
                        sx={{ ml: 'auto', width: 28, height: 28, border: '1px dashed', borderColor: 'primary.main', color: 'primary.main', opacity: 0.6, '&:hover': { opacity: 1 } }}
                      >
                        <AddIcon sx={{ fontSize: 16 }} />
                      </IconButton>
                    ) : null}
                  </Stack>
                  {selectedTagIds.length > 0 ? (
                    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.75 }}>
                      {selectedTagIds.map((tagId) => {
                        const tag = tags.find((item) => item.id === tagId)
                        if (!tag) {
                          return (
                            <Chip
                              key={tagId}
                              size="small"
                              label={tagId}
                              onDelete={canManageCards && !isBusy ? () => handleRemoveTag(tagId) : undefined}
                            />
                          )
                        }
                        return (
                          <TagChip
                            key={tagId}
                            tag={{ name: tag.name, color: tag.color }}
                            onDelete={canManageCards && !isBusy ? () => handleRemoveTag(tagId) : undefined}
                          />
                        )
                      })}
                    </Box>
                  ) : null}
                  <Menu
                    anchorEl={tagMenuAnchor}
                    open={Boolean(tagMenuAnchor)}
                    onClose={() => setTagMenuAnchor(null)}
                  >
                    {availableTagsForMenu.map((tag) => (
                      <MenuItem key={tag.id} onClick={() => handleAddTag(tag.id)}>
                        {tag.name}
                      </MenuItem>
                    ))}
                  </Menu>
                </Stack>

                {/* Dates */}
                <Divider />
                <Stack spacing={0}>
                  {/* Start Date */}
                  <Stack
                    direction="row"
                    alignItems="center"
                    onClick={canManageCards && !isBusy ? (e) => setStartDateAnchor(e.currentTarget) : undefined}
                    sx={{ px: 2, py: 1.25, cursor: canManageCards && !isBusy ? 'pointer' : 'default', '&:hover': canManageCards && !isBusy ? { bgcolor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.06)' : 'rgba(13, 148, 136, 0.04)' } : {} }}
                  >
                    <CalendarTodayIcon sx={{ fontSize: 14, color: 'primary.main', mr: 1, opacity: 0.5 }} />
                    <Typography variant="body2" color="text.secondary" sx={{ flex: 1, fontSize: '0.8125rem' }}>Start</Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ fontSize: '0.8125rem' }}>
                      {startDateDraft || '—'}
                    </Typography>
                  </Stack>
                  <Popover
                    open={Boolean(startDateAnchor)}
                    anchorEl={startDateAnchor}
                    onClose={() => setStartDateAnchor(null)}
                    anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
                  >
                    <DateCalendar
                      value={startDateDraft ? parseISO(startDateDraft) : null}
                      onChange={(val) => {
                        setStartDateDraft(val ? formatDateFns(val, 'yyyy-MM-dd') : '')
                        setStartDateAnchor(null)
                      }}
                    />
                    <DialogActions sx={{ pt: 0 }}>
                      <Button
                        size="small"
                        onClick={() => {
                          setStartDateDraft('')
                          setStartDateAnchor(null)
                        }}
                      >
                        Clear
                      </Button>
                    </DialogActions>
                  </Popover>
                  {/* Due Date */}
                  <Stack
                    direction="row"
                    alignItems="center"
                    onClick={canManageCards && !isBusy ? (e) => setDueDateAnchor(e.currentTarget) : undefined}
                    sx={{ px: 2, py: 1.25, cursor: canManageCards && !isBusy ? 'pointer' : 'default', '&:hover': canManageCards && !isBusy ? { bgcolor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.06)' : 'rgba(13, 148, 136, 0.04)' } : {} }}
                  >
                    <CalendarTodayIcon sx={{ fontSize: 14, color: dueDateDraft && isOverdue(dueDateDraft) ? 'error.main' : 'primary.main', mr: 1, opacity: dueDateDraft && isOverdue(dueDateDraft) ? 1 : 0.5 }} />
                    <Typography variant="body2" color="text.secondary" sx={{ flex: 1, fontSize: '0.8125rem' }}>Due</Typography>
                    <Typography
                      variant="body2"
                      sx={{
                        fontSize: '0.8125rem',
                        color: dueDateDraft && isOverdue(dueDateDraft) ? 'error.main' : 'text.secondary',
                      }}
                    >
                      {dueDateDraft || '—'}
                    </Typography>
                  </Stack>
                  <Popover
                    open={Boolean(dueDateAnchor)}
                    anchorEl={dueDateAnchor}
                    onClose={() => setDueDateAnchor(null)}
                    anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
                  >
                    <DateCalendar
                      value={dueDateDraft ? parseISO(dueDateDraft) : null}
                      onChange={(val) => {
                        setDueDateDraft(val ? formatDateFns(val, 'yyyy-MM-dd') : '')
                        setDueDateAnchor(null)
                      }}
                    />
                    <DialogActions sx={{ pt: 0 }}>
                      <Button
                        size="small"
                        onClick={() => {
                          setDueDateDraft('')
                          setDueDateAnchor(null)
                        }}
                      >
                        Clear
                      </Button>
                    </DialogActions>
                  </Popover>
                  {dateError ? (
                    <Typography variant="caption" color="error" sx={{ px: 2, pb: 1 }}>
                      {dateError}
                    </Typography>
                  ) : null}
                  {/* Estimated Hours */}
                  <Stack
                    direction="row"
                    alignItems="center"
                    onClick={canManageCards && !isBusy ? (e) => setEstimatedHoursAnchor(e.currentTarget) : undefined}
                    sx={{
                      px: 2,
                      py: 1.25,
                      cursor: canManageCards && !isBusy ? 'pointer' : 'default',
                      '&:hover': canManageCards && !isBusy ? { bgcolor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.06)' : 'rgba(13, 148, 136, 0.04)' } : {},
                    }}
                  >
                    <AccessTimeIcon sx={{ fontSize: 14, color: 'primary.main', mr: 1, opacity: 0.5 }} />
                    <Typography variant="body2" color="text.secondary" sx={{ flex: 1, fontSize: '0.8125rem' }}>
                      Estimated
                    </Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ fontSize: '0.8125rem' }}>
                      {estimatedHoursDraft.trim().length > 0 && !estimatedHoursError
                        ? `${Number(estimatedHoursDraft)}h`
                        : '—'}
                    </Typography>
                  </Stack>
                  <Popover
                    open={Boolean(estimatedHoursAnchor)}
                    anchorEl={estimatedHoursAnchor}
                    onClose={() => setEstimatedHoursAnchor(null)}
                    anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
                    transformOrigin={{ vertical: 'top', horizontal: 'right' }}
                  >
                    <Box sx={{ p: 2, width: 220 }}>
                      <TextField
                        value={estimatedHoursDraft}
                        onChange={(event) => setEstimatedHoursDraft(event.target.value)}
                        disabled={!canManageCards || isBusy}
                        size="small"
                        fullWidth
                        autoFocus
                        type="number"
                        label="Estimated hours"
                        placeholder="0"
                        error={Boolean(estimatedHoursError)}
                        helperText={estimatedHoursError ?? ' '}
                        inputProps={{ min: 0, step: 0.25, 'aria-label': 'Estimated hours' }}
                        InputProps={{
                          endAdornment: <Typography variant="body2" color="text.disabled">h</Typography>,
                        }}
                        onKeyDown={(event) => {
                          if (event.key === 'Enter' && !estimatedHoursError) {
                            setEstimatedHoursAnchor(null)
                          }
                        }}
                      />
                    </Box>
                    <DialogActions sx={{ pt: 0 }}>
                      <Button
                        size="small"
                        onClick={() => {
                          setEstimatedHoursDraft('')
                          setEstimatedHoursAnchor(null)
                        }}
                      >
                        Clear
                      </Button>
                    </DialogActions>
                  </Popover>
                </Stack>

                {/* Created */}
                <Divider />
                <Stack direction="row" alignItems="center" sx={{ px: 2, py: 1.25 }}>
                  <Typography variant="body2" color="text.disabled" sx={{ flex: 1, fontSize: '0.8125rem' }}>Created</Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ fontSize: '0.8125rem' }}>
                    {formatDateShort(card.createdAt)}
                  </Typography>
                </Stack>
              </Box>

              {/* Files section */}
              <Box sx={{
                bgcolor: 'background.paper',
                borderRadius: 2,
                border: '1px solid',
                borderColor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.15)' : 'rgba(13, 148, 136, 0.12)',
                overflow: 'hidden',
                mt: 2,
              }}>
                <Stack direction="row" alignItems="center" sx={{ px: 2, pt: 1.5, pb: 1 }}>
                  <AttachFileIcon sx={{ fontSize: 16, color: 'primary.main', mr: 0.75, opacity: 0.7 }} />
                  <Typography variant="caption" fontWeight={700} color="primary.main" sx={{ letterSpacing: '0.05em', textTransform: 'uppercase', fontSize: '0.6875rem' }}>
                    Attachments
                  </Typography>
                  <Typography variant="caption" color="text.disabled" sx={{ ml: 0.75, fontSize: '0.6875rem' }}>
                    {(card.attachments ?? []).length}
                  </Typography>
                </Stack>

                {(card.attachments ?? []).length > 0 ? (
                  <Stack spacing={0}>
                    {(card.attachments ?? []).map((attachment) => (
                      <Box
                        key={attachment.id}
                        onClick={() => void handleDownloadAttachment(attachment)}
                        sx={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: 1.5,
                          px: 2,
                          py: 1.25,
                          overflow: 'hidden',
                          cursor: 'pointer',
                          '&:hover': { bgcolor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.06)' : 'rgba(13, 148, 136, 0.04)' },
                          '&:hover .attachment-actions': { opacity: 1 },
                        }}
                      >
                        <Box sx={{ color: 'text.disabled', display: 'flex' }}>
                          {attachment.mimeType?.startsWith('image/') ? (
                            <ImageIcon sx={{ fontSize: 16 }} />
                          ) : attachment.mimeType === 'application/pdf' ? (
                            <PictureAsPdfIcon sx={{ fontSize: 16 }} />
                          ) : (
                            <InsertDriveFileIcon sx={{ fontSize: 16 }} />
                          )}
                        </Box>
                        <Box sx={{ flex: 1, minWidth: 0, overflow: 'hidden', display: 'flex', flexDirection: 'column', justifyContent: 'space-between', alignSelf: 'stretch' }}>
                          <Typography variant="caption" noWrap title={attachment.filename} sx={{ display: 'block', fontWeight: 500 }}>{attachment.filename}</Typography>
                          <Box>
                            <Typography variant="caption" color="text.disabled" sx={{ fontSize: '0.625rem', display: 'block' }}>
                              {formatFileSize(attachment.fileSize)}
                            </Typography>
                            <Typography variant="caption" color="text.disabled" sx={{ fontSize: '0.625rem', display: 'block' }}>
                              Added {formatDateShort(attachment.uploadedAt)}
                            </Typography>
                            {attachment.uploader ? (
                              <Typography variant="caption" color="text.disabled" sx={{ fontSize: '0.625rem', display: 'block' }}>
                                Uploaded by {attachment.uploader.userName ?? attachment.uploader.email}
                              </Typography>
                            ) : null}
                          </Box>
                        </Box>
                        {canDeleteAttachment(attachment) ? (
                          <Box className="attachment-actions" sx={{ opacity: 0, flexShrink: 0 }} onClick={(e) => e.stopPropagation()}>
                            <IconButton
                              size="small"
                              onClick={() => void handleDeleteAttachment(attachment)}
                              disabled={isDeletingAttachment}
                              aria-label={`Delete ${attachment.filename}`}
                              color="error"
                              sx={{ p: 0.5 }}
                            >
                              <DeleteOutlineIcon sx={{ fontSize: 16 }} />
                            </IconButton>
                          </Box>
                        ) : null}
                      </Box>
                    ))}
                  </Stack>
                ) : null}

                {isUploadingAttachment && uploadingFileName ? (
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 2, py: 1 }}>
                    <CircularProgress size={14} />
                    <Typography variant="caption" color="text.secondary">
                      Uploading {uploadingFileName}...
                    </Typography>
                  </Box>
                ) : null}

                {canManageCards ? (
                  <Box
                    onDragOver={handleDragOver}
                    onDragLeave={handleDragLeave}
                    onDrop={handleDrop}
                    onClick={() => fileInputRef.current?.click()}
                    sx={{
                      mx: 2,
                      mb: 1.5,
                      mt: 0.5,
                      border: '1px dashed',
                      borderColor: isDragOver ? 'primary.main' : (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.25)' : 'rgba(13, 148, 136, 0.2)',
                      borderRadius: 1,
                      p: 1.25,
                      textAlign: 'center',
                      cursor: isBusy ? 'default' : 'pointer',
                      bgcolor: isDragOver ? (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.08)' : 'rgba(13, 148, 136, 0.05)' : 'transparent',
                      opacity: isBusy ? 0.5 : 1,
                      '&:hover': { borderColor: 'primary.main' },
                    }}
                  >
                    <CloudUploadIcon sx={{ color: 'primary.main', fontSize: 18, opacity: 0.6 }} />
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontSize: '0.75rem' }}>
                      Drop or click
                    </Typography>
                    <input
                      ref={fileInputRef}
                      type="file"
                      hidden
                      onChange={handleFileInputChange}
                      disabled={isBusy}
                    />
                  </Box>
                ) : null}

                {attachmentError ? (
                  <Alert severity="error" onClose={() => setAttachmentError(null)} sx={{ mx: 2, mb: 1.5, fontSize: '0.75rem' }}>
                    {attachmentError}
                  </Alert>
                ) : null}

                {/* Google Drive Links */}
                <GoogleDriveLinksSection
                  cardId={card.id}
                  canManageCards={canManageCards}
                  currentUserId={currentUserId}
                  isCardCreator={isCardCreator}
                  isProjectManager={isProjectManager}
                />
              </Box>

            </Stack>
          </Box>
        </Box>
      </DialogContent>

      {/* Footer actions */}
      {canManageCards ? (
        <DialogActions sx={{ px: 3, py: 2, borderTop: '1px solid', borderColor: (theme) => theme.palette.mode === 'dark' ? 'rgba(20, 184, 166, 0.12)' : 'rgba(13, 148, 136, 0.1)' }}>
          <Button
            variant="outlined"
            color="warning"
            startIcon={<ArchiveIcon />}
            onClick={handleArchive}
            disabled={isBusy}
            size="small"
            sx={{ mr: 'auto', textTransform: 'none', fontWeight: 500 }}
          >
            {isArchiving ? 'Archiving...' : 'Archive Task'}
          </Button>
          {hasAnyChanges ? (
            <Typography variant="caption" color="text.secondary">
              Unsaved changes
            </Typography>
          ) : null}
          <Button onClick={handleClose} disabled={isBusy} size="small">
            Cancel
          </Button>
          <Button onClick={handleSaveChanges} variant="contained" disabled={!canSaveChanges || isBusy} size="small">
            {isBusy ? 'Saving...' : 'Save Changes'}
          </Button>
        </DialogActions>
      ) : null}
    </>
  )
}

export function CardDetailDialog({ open, cardId, boardId, columns = [], members, canManageCards, currentUserRole, onClose }: CardDetailDialogProps) {
  const cardQuery = useCard(open ? cardId ?? undefined : undefined)
  const card = cardQuery.data
  const tagsQuery = useBoardTags(open ? boardId : undefined)
  const tags = useMemo(() => tagsQuery.data ?? [], [tagsQuery.data])

  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="lg"
      fullScreen={typeof window !== 'undefined' && window.innerWidth < 600}
      slotProps={{ paper: { sx: { bgcolor: 'background.default', maxHeight: { xs: '100vh', sm: '90vh' } } } }}
    >
      {cardQuery.isLoading ? <CardDetailSkeleton /> : null}

      {cardQuery.isError ? (
        <DialogContent>
          <Alert severity="error">Unable to load task details.</Alert>
        </DialogContent>
      ) : null}

      {card ? (
        <CardDetailEditor
          key={`${card.id}-${card.version}`}
          card={card}
          columns={columns}
          tags={tags}
          members={members}
          canManageCards={canManageCards}
          currentUserRole={currentUserRole}
          onClose={onClose}
          onRefresh={() => cardQuery.refetch()}
        />
      ) : null}
    </Dialog>
  )
}
