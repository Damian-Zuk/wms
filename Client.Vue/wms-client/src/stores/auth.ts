import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { accountsApi } from '@/api/endpoints/accounts'
import { clearStoredAuth, readStoredAuth, writeStoredAuth } from '@/api/auth-token'
import type { LoginRequest, UserDto } from '@/types/auth'

export const useAuthStore = defineStore('auth', () => {
  // Rehydrate from localStorage so a refresh keeps the session.
  const stored = readStoredAuth()
  const token = ref<string | null>(stored?.token ?? null)
  const expiresAt = ref<string | null>(stored?.expiresAt ?? null)
  const user = ref<UserDto | null>(stored?.user ?? null)

  const isAuthenticated = computed(() => {
    if (!token.value || !expiresAt.value) return false
    return new Date(expiresAt.value).getTime() > Date.now()
  })

  const roles = computed(() => user.value?.roles ?? [])
  const hasRole = (role: string) => roles.value.includes(role)
  const canMutate = computed(() => hasRole('Admin') || hasRole('Manager'))

  async function login(credentials: LoginRequest): Promise<void> {
    const res = await accountsApi.login(credentials)
    token.value = res.token
    expiresAt.value = res.expiresAt
    user.value = res.user
    writeStoredAuth({ token: res.token, expiresAt: res.expiresAt, user: res.user })
  }

  function logout(): void {
    token.value = null
    expiresAt.value = null
    user.value = null
    clearStoredAuth()
  }

  return {
    token,
    expiresAt,
    user,
    isAuthenticated,
    roles,
    hasRole,
    canMutate,
    login,
    logout,
  }
})
