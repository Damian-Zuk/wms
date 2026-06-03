import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { accountsApi } from '@/api/endpoints/accounts'
import { qk } from '@/api/query-keys'
import type { ChangePasswordRequest, RegisterRequest } from '@/types/auth'

export function useUsers() {
  return useQuery({
    queryKey: qk.users.list(),
    queryFn: () => accountsApi.list(),
    staleTime: 60_000,
  })
}

export function useCreateUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: RegisterRequest) => accountsApi.register(body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.users.all })
    },
  })
}

export function useChangePassword() {
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: ChangePasswordRequest }) =>
      accountsApi.changePassword(id, body),
  })
}

export function useDeleteUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => accountsApi.remove(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.users.all })
    },
  })
}
