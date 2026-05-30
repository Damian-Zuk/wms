import { computed, type Ref } from 'vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import { productsApi } from '@/api/endpoints/products'
import { qk } from '@/api/query-keys'
import type { ProductFilters } from '@/types/products'

export function useProducts(filters: Ref<ProductFilters>) {
  return useQuery({
    queryKey: computed(() => qk.products.list(filters.value)),
    queryFn: () => productsApi.list(filters.value),
    placeholderData: keepPreviousData,
    staleTime: 5 * 60_000,
  })
}
