import type { IsoDateString } from './common'

export interface PaginatedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface ValidationErrorItem {
  propertyName: string
  errorMessage: string
  attemptedValue: unknown
}

export interface ErrorDetails {
  code: string
  message: string
  timestamp: IsoDateString
  requestId: string
  validationErrors?: ValidationErrorItem[] | null
}

export interface ErrorResponse {
  error: ErrorDetails
}
