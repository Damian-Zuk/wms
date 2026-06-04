import { computed, type Ref } from 'vue'
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/vue-query'
import { inventoryApi } from '@/api/endpoints/inventory'
import { stockTransfersApi } from '@/api/endpoints/stock-transfers'
import { qk } from '@/api/query-keys'
import type {
  AdjustInventoryRequest,
  AvailabilityParams,
  InventoryFilters,
} from '@/types/inventory'
import type { TransferStockRequest } from '@/types/stock-transfers'

// Inventory is correctness-critical: never serve stale on-hand/available
// numbers. staleTime 0 forces a refetch whenever a view mounts/refocuses.

export function useInventories(filters: Ref<InventoryFilters>) {
  return useQuery({
    queryKey: computed(() => qk.inventories.list(filters.value)),
    queryFn: () => inventoryApi.list(filters.value),
    placeholderData: keepPreviousData,
    staleTime: 0,
  })
}

export function useInventory(id: Ref<string>) {
  return useQuery({
    queryKey: computed(() => qk.inventories.detail(id.value)),
    queryFn: () => inventoryApi.get(id.value),
    enabled: computed(() => !!id.value),
    staleTime: 0,
  })
}

export function useAvailability(params: Ref<AvailabilityParams>) {
  return useQuery({
    queryKey: computed(() => qk.inventories.availability(params.value)),
    queryFn: () => inventoryApi.availability(params.value),
    enabled: computed(() => !!params.value.productId),
    staleTime: 0,
  })
}

export function useExpiringInventory(withinDays: Ref<number>) {
  return useQuery({
    queryKey: computed(() => qk.inventories.expiring(withinDays.value)),
    queryFn: () => inventoryApi.expiring(withinDays.value),
    staleTime: 0,
  })
}

export function useAdjustInventory() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (vars: { id: string; body: AdjustInventoryRequest }) =>
      inventoryApi.adjust(vars.id, vars.body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.inventories.all })
    },
  })
}

export function useTransferStock() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: TransferStockRequest) => stockTransfersApi.create(body),
    onSuccess: () => {

      void qc.invalidateQueries({ queryKey: qk.inventories.all })
    },
  })
}
