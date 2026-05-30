import { computed, type Ref } from 'vue'
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/vue-query'
import { stockInsApi } from '@/api/endpoints/stock-ins'
import { qk } from '@/api/query-keys'
import type { CreateStockInCommand, StockInFilters } from '@/types/stock-ins'

// Workflow statuses change over time, so never serve stale documents.

export function useStockIns(filters: Ref<StockInFilters>) {
  return useQuery({
    queryKey: computed(() => qk.stockIns.list(filters.value)),
    queryFn: () => stockInsApi.list(filters.value),
    placeholderData: keepPreviousData,
    staleTime: 0,
  })
}

export function useStockIn(id: Ref<string>) {
  return useQuery({
    queryKey: computed(() => qk.stockIns.detail(id.value)),
    queryFn: () => stockInsApi.get(id.value),
    enabled: computed(() => !!id.value),
    staleTime: 0,
  })
}

export function useCreateStockIn() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateStockInCommand) => stockInsApi.create(body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.stockIns.all })
    },
  })
}

export type StockInAction = 'startReceiving' | 'receive' | 'complete' | 'cancel'

export function useStockInTransition() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (vars: { id: string; action: StockInAction }) =>
      stockInsApi[vars.action](vars.id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.stockIns.all })
      // `receive` books stock; invalidating inventory keeps those views truthful.
      void qc.invalidateQueries({ queryKey: qk.inventories.all })
    },
  })
}
