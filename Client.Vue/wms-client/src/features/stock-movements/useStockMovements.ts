import { computed, type Ref } from 'vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import { stockMovementsApi } from '@/api/endpoints/stock-movements'
import { qk } from '@/api/query-keys'
import type { StockMovementFilters } from '@/types/stock-movements'

// Stock movements are an immutable audit log, so cached pages stay valid.

export function useStockMovements(filters: Ref<StockMovementFilters>) {
  return useQuery({
    queryKey: computed(() => qk.stockMovements.list(filters.value)),
    queryFn: () => stockMovementsApi.list(filters.value),
    placeholderData: keepPreviousData,
  })
}

export function useStockMovement(id: Ref<string>) {
  return useQuery({
    queryKey: computed(() => qk.stockMovements.detail(id.value)),
    queryFn: () => stockMovementsApi.get(id.value),
    enabled: computed(() => !!id.value),
  })
}
