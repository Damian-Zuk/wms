import type { UserDto } from '@/types/auth'

const STORAGE_KEY = 'wms.auth'

export interface StoredAuth {
  token: string
  expiresAt: string
  user: UserDto
}

export function readStoredAuth(): StoredAuth | null {
  const raw = localStorage.getItem(STORAGE_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as StoredAuth
  } catch {
    return null
  }
}

export function writeStoredAuth(data: StoredAuth): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(data))
}

export function clearStoredAuth(): void {
  localStorage.removeItem(STORAGE_KEY)
}

export function getToken(): string | null {
  return readStoredAuth()?.token ?? null
}
