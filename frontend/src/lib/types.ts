export type { Guid, IsoDateString } from '@/types/common'
export type {
  PaginatedResponse,
  ValidationErrorItem,
  ErrorDetails,
  ErrorResponse,
} from '@/types/api'

import type { Guid, IsoDateString } from '@/types/common'

export type ProjectRole = 0 | 1 | 2 | 3

export type NotificationType = 0 | 1 | 2 | 3 | 4 | 5

export type EntityType = 0 | 1 | 2 | 3

export interface User {
  id: Guid
  email?: string | null
  userName?: string | null
  createdAt?: IsoDateString
  updatedAt?: IsoDateString
}

export interface Project {
  id: Guid
  name: string
  ownerId: Guid
  memberCount?: number
  boardCount?: number
  owner?: User
  createdAt: IsoDateString
  updatedAt: IsoDateString
  deletedAt?: IsoDateString | null
  members?: ProjectMember[]
  boards?: Board[]
  invitations?: Invitation[]
}

export interface ProjectMember {
  id?: Guid
  projectId?: Guid
  userId: Guid
  email?: string
  userName?: string | null
  role: ProjectRole
  joinedAt: IsoDateString
  project?: Project
  user?: User
}

export interface Board {
  id: Guid
  projectId: Guid
  project?: Project
  name: string
  position: number
  createdAt: IsoDateString
  updatedAt: IsoDateString
  deletedAt?: IsoDateString | null
  columns?: Column[]
  columnCount?: number
  cardCount?: number
}

export interface Column {
  id: Guid
  boardId: Guid
  board?: Board
  name: string
  position: number
  createdAt: IsoDateString
  updatedAt: IsoDateString
  deletedAt?: IsoDateString | null
  cards?: Card[]
}

export interface Card {
  id: Guid
  columnId: Guid
  column?: Column
  title: string
  description?: string | null
  position: number
  startDate?: IsoDateString | null
  dueDate?: IsoDateString | null
  estimatedHours?: number | null
  scheduledMinutes?: number
  spentMinutes?: number
  version: number
  createdAt: IsoDateString
  updatedAt: IsoDateString
  deletedAt?: IsoDateString | null
  createdBy?: Guid | null
  creator?: User | null
  cardTags?: CardTag[]
  assignments?: CardAssignment[]
  attachments?: Attachment[]
  subtasks?: Subtask[]
  comments?: Comment[]
  googleDriveLinks?: GoogleDriveLink[]
}

export interface CardTag {
  id: Guid
  cardId: Guid
  tagId: Guid
  createdAt: IsoDateString
  card?: Card
  tag?: Tag
}

export interface CardAssignment {
  id: Guid
  cardId: Guid
  userId: Guid
  assignedAt: IsoDateString
  assignedBy?: Guid | null
  card?: Card
  user?: User
  assigner?: User | null
}

export interface Tag {
  id: Guid
  boardId: Guid
  board?: Board
  name: string
  color: string
  createdAt: IsoDateString
  cardTags?: CardTag[]
}

export interface Attachment {
  id: Guid
  cardId: Guid
  card?: Card
  filename: string
  fileSize: number
  storageKey: string
  mimeType?: string | null
  uploadedBy?: Guid | null
  uploader?: User | null
  uploadedAt: IsoDateString
  deletedAt?: IsoDateString | null
}

export interface Subtask {
  id: Guid
  cardId: Guid
  card?: Card
  description: string
  completed: boolean
  position: number
  createdAt: IsoDateString
  updatedAt: IsoDateString
  deletedAt?: IsoDateString | null
}

export interface Comment {
  id: Guid
  cardId: Guid
  authorId: Guid
  author?: User
  content: string
  createdAt: IsoDateString
  updatedAt: IsoDateString
  deletedAt?: IsoDateString | null
}

export interface CardActivity {
  id: Guid
  cardId: Guid
  userId: Guid
  user?: User
  action: string
  field?: string | null
  oldValue?: string | null
  newValue?: string | null
  createdAt: IsoDateString
}

export interface ProjectActivity {
  id: Guid
  entityType: 'card' | 'board' | 'workspace'
  cardId?: Guid | null
  cardTitle?: string | null
  boardId?: Guid | null
  boardName?: string | null
  columnName?: string | null
  userId: Guid
  userName: string
  action: string
  field?: string | null
  oldValue?: string | null
  newValue?: string | null
  createdAt: IsoDateString
  entityName: string
}

export interface Notification {
  id: Guid
  userId: Guid
  user?: User
  type: NotificationType
  title: string
  message: string
  entityType?: string | null
  entityId?: Guid | null
  isRead: boolean
  createdAt: IsoDateString
  createdBy?: Guid | null
  creator?: User | null
}

export interface Subscription {
  id: Guid
  userId: Guid
  user?: User
  entityType: EntityType
  entityId: Guid
  createdAt: IsoDateString
}

export interface Invitation {
  id: Guid
  projectId: Guid
  project?: Project
  email: string
  token: string
  invitedBy: Guid
  inviter?: User
  createdAt: IsoDateString
  expiresAt: IsoDateString
  acceptedAt?: IsoDateString | null
  acceptedBy?: Guid | null
  accepter?: User | null
}

export interface SwimlaneView {
  projectId: Guid
  boards: BoardSwimlane[]
}

export interface BoardSwimlane {
  board: Board
  columns: ColumnSwimlane[]
}

export interface ColumnSwimlane {
  column: Column
  cards: Card[]
  cardCount: number
}

export interface GlobalSearchResult {
  items: GlobalSearchItem[]
}

export interface GlobalSearchItem {
  id: Guid
  type: 'project' | 'board' | 'column' | 'card'
  name: string
  description?: string | null
  location?: GlobalSearchItemLocation | null
}

export interface GlobalSearchItemLocation {
  projectId?: Guid | null
  projectName?: string | null
  boardId?: Guid | null
  boardName?: string | null
  columnId?: Guid | null
  columnName?: string | null
}

export interface FilterCriteria {
  tagIds?: Guid[]
  userIds?: Guid[]
}

export interface CreateCardData {
  title: string
  description?: string | null
  startDate?: IsoDateString | null
  dueDate?: IsoDateString | null
  estimatedHours?: number | null
  tagIds?: Guid[]
  assigneeUserIds?: Guid[]
}

export interface UpdateCardData {
  title: string
  description?: string | null
  startDate?: IsoDateString | null
  dueDate?: IsoDateString | null
  estimatedHours?: number | null
  version: number
}

export interface ScheduleCardData {
  startDate: IsoDateString | null
  dueDate: IsoDateString | null
}

export interface MoveCardData {
  columnId: Guid
  position: number
}

export interface AuthResult {
  userId: Guid
  email: string
  userName?: string | null
  accessToken: string
  accessTokenExpiresAt: IsoDateString
  refreshToken: string
  refreshTokenExpiresAt: IsoDateString
}

export interface GoogleConnectionStatus {
  connected: boolean
  googleEmail?: string | null
  connectedAt?: IsoDateString | null
}

export type DriveSharePermission = 'reader' | 'commenter' | 'writer'

export interface GoogleDriveLink {
  id: Guid
  googleFileId: string
  name: string
  mimeType: string
  webViewLink: string
  iconLink?: string | null
  thumbnailLink?: string | null
  fileSize?: number | null
  googleModifiedAt?: IsoDateString | null
  linkedBy: Guid
  linkedByUserName: string
  linkedAt: IsoDateString
  sharePermission: DriveSharePermission
}

export interface PermissionReport {
  sharedCount: number
  alreadySharedCount: number
  failedCount: number
  failedEmails: string[]
  // True when a linked file could not be shared with the team because the current user
  // is not the owner / lacks sharing rights. The file link is still saved.
  shareNotAllowed: boolean
  unshareableFileNames: string[]
}

export interface LinkFilesResult {
  links: GoogleDriveLink[]
  permissionReport: PermissionReport
}

export interface PermissionRevokeReport {
  revokedCount: number
  failedCount: number
  failedEmails: string[]
}

export type PlannedBlockSyncStatus = 0 | 1 | 2

export interface PlannedBlock {
  id: Guid
  cardId: Guid
  cardTitle: string
  projectId: Guid
  date: string
  startTime: string
  endTime: string
  syncStatus: PlannedBlockSyncStatus
  googleEventId?: string | null
}

export interface CreateBlockData {
  cardId: Guid
  date: string
  startTime: string
  endTime: string
  timeZone: string
}

export interface UpdateBlockData {
  date?: string
  startTime?: string
  endTime?: string
  timeZone?: string
}

export interface UnscheduledCard {
  id: Guid
  title: string
  description?: string | null
  columnId: Guid
  columnName: string
  boardId: Guid
  boardName: string
}

export interface GoogleCalendarEvent {
  id: string
  summary: string
  start: IsoDateString
  end: IsoDateString
  htmlLink?: string | null
}

export interface FavoriteDto {
  id: Guid
  entityType: EntityType
  entityId: Guid
  createdAt: IsoDateString
}

export interface MySubscriptionDto {
  id: Guid
  entityType: EntityType
  entityId: Guid
  entityName: string
  projectName: string | null
  projectId: Guid | null
  boardId: Guid | null
  boardName: string | null
  columnName: string | null
  createdAt: IsoDateString
}
