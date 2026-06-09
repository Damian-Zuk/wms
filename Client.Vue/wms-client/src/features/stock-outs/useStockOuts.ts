import { computed, type Ref } from 'vue'
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/vue-query'
import { stockOutsApi } from '@/api/endpoints/stock-outs'
import { qk } from '@/api/query-keys'
import type {
  CreateStockOutCommand,
  PickAllocationInput,
  StockOutFilters,
} from '@/types/stock-outs'
import type { PickingStrategyType } from '@/types/enums'

// Workflow statuses change over time, so never serve stale documents.

export function useStockOuts(filters: Ref<StockOutFilters>) {
  return useQuery({
    queryKey: computed(() => qk.stockOuts.list(filters.value)),
    queryFn: () => stockOutsApi.list(filters.value),
    placeholderData: keepPreviousData,
    staleTime: 0,
  })
}

export function useStockOut(id: Ref<string>) {
  return useQuery({
    queryKey: computed(() => qk.stockOuts.detail(id.value)),
    queryFn: () => stockOutsApi.get(id.value),
    enabled: computed(() => !!id.value),
    staleTime: 0,
  })
}

export function useCreateStockOut() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateStockOutCommand) => stockOutsApi.create(body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.stockOuts.all })
      // Creating reserves stock; keep inventory views truthful.
      void qc.invalidateQueries({ queryKey: qk.inventories.all })
    },
  })
}

export function useModifyPickLocations(stockOutId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (vars: { lineId: string; allocations: PickAllocationInput[] }) =>
      stockOutsApi.modifyPickLocations(stockOutId, vars.lineId, {
        allocations: vars.allocations,
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.stockOuts.detail(stockOutId) })
      void qc.invalidateQueries({ queryKey: qk.stockOuts.all })
      // Re-planning moves reservations between sources; keep inventory views truthful.
      void qc.invalidateQueries({ queryKey: qk.inventories.all })
    },
  })
}

export function useReplanLine(stockOutId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (vars: { lineId: string; strategy: PickingStrategyType }) =>
      stockOutsApi.replanLine(stockOutId, vars.lineId, { strategy: vars.strategy }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.stockOuts.detail(stockOutId) })
      void qc.invalidateQueries({ queryKey: qk.stockOuts.all })
      // Re-planning moves reservations between sources; keep inventory views truthful.
      void qc.invalidateQueries({ queryKey: qk.inventories.all })
    },
  })
}

export type StockOutAction = 'startPicking' | 'complete' | 'cancel'

export function useStockOutTransition() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (vars: { id: string; action: StockOutAction }) =>
      stockOutsApi[vars.action](vars.id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.stockOuts.all })
      // Cancelling can release/return stock; keep inventory views truthful.
      void qc.invalidateQueries({ queryKey: qk.inventories.all })
    },
  })
}

export function usePickItem(stockOutId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (vars: { itemId: string; quantity: number }) =>
      stockOutsApi.pickItem(stockOutId, vars.itemId, { quantity: vars.quantity }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.stockOuts.detail(stockOutId) })
      void qc.invalidateQueries({ queryKey: qk.stockOuts.all })
      // Each pick removes stock; refresh inventory views.
      void qc.invalidateQueries({ queryKey: qk.inventories.all })
    },
  })
}
