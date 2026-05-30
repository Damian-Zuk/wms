export interface AppError {
  status: number
  code: string
  message: string
  fieldErrors: Record<string, string[]>
  raw: unknown
}

export function normalizeError(error: unknown): AppError {
  const err = error as {
    response?: { status?: number; data?: Record<string, unknown> }
    message?: string
  }
  const response = err?.response
  const data = (response?.data ?? {}) as Record<string, unknown>
  const status = response?.status ?? 0

  const message =
    (data.detail as string) ??
    (data.message as string) ??
    err?.message ??
    'An unexpected error occurred'

  const code = (data.title as string) ?? (status ? `Http.${status}` : 'Network.Error')

  const fieldErrors =
    data.errors && typeof data.errors === 'object'
      ? (data.errors as Record<string, string[]>)
      : {}

  return { status, code, message, fieldErrors, raw: error }
}
