import { computed, type Ref } from 'vue'
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/vue-query'
import { lotsApi } from '@/api/endpoints/lots'
import { qk } from '@/api/query-keys'
import type { CreateLotCommand, LotFilters, UpdateLotRequest } from '@/types/lots'

export function useLots(filters: Ref<LotFilters>) {
  return useQuery({
    queryKey: computed(() => qk.lots.list(filters.value)),
    queryFn: () => lotsApi.list(filters.value),
    placeholderData: keepPreviousData,
    staleTime: 5 * 60_000,
  })
}

export function useLot(id: Ref<string>) {
  return useQuery({
    queryKey: computed(() => qk.lots.detail(id.value)),
    queryFn: () => lotsApi.get(id.value),
    enabled: computed(() => !!id.value),
  })
}

/**
 * Loads lots for a given product as select options. Disabled (and empty)
 * until a product is chosen; keyed by productId so each scope caches separately.
 */
export function useLotOptions(productId: Ref<string | undefined>) {
  const query = useQuery({
    queryKey: computed(() => qk.lots.options(productId.value)),
    queryFn: () => lotsApi.list({ productId: productId.value, page: 1, pageSize: 200 }),
    enabled: computed(() => !!productId.value),
    staleTime: 5 * 60_000,
  })

  const options = computed(() =>
    (query.data.value?.items ?? []).map((l) => ({ label: l.number, value: l.id })),
  )

  return { ...query, options }
}

export function useCreateLot() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateLotCommand) => lotsApi.create(body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.lots.all })
    },
  })
}

export function useUpdateLot(id: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateLotRequest) => lotsApi.update(id, body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.lots.all })
      void qc.invalidateQueries({ queryKey: qk.lots.detail(id) })
    },
  })
}

export function useDeleteLot() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => lotsApi.remove(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.lots.all })
    },
  })
}
