import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { adminApi } from '@/api/endpoints/admin'

export function useSeedData() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => adminApi.seed(),
    // Seeding touches every entity, so refresh the whole cache.
    onSuccess: () => {
      void qc.invalidateQueries()
    },
  })
}

export function useTruncateData() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => adminApi.truncate(),
    // Truncation clears every entity, so refresh the whole cache.
    onSuccess: () => {
      void qc.invalidateQueries()
    },
  })
}
