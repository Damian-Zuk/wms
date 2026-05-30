import type { AppError } from '@/api/problem-details'

declare module '@tanstack/query-core' {
  interface Register {
    defaultError: AppError
  }
}
