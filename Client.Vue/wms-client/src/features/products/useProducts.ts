import { computed, type Ref } from 'vue'
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/vue-query'
import { productsApi } from '@/api/endpoints/products'
import { qk } from '@/api/query-keys'
import type {
  CreateProductCommand,
  ProductFilters,
  UpdateProductRequest,
} from '@/types/products'

export function useProducts(filters: Ref<ProductFilters>) {
  return useQuery({
    queryKey: computed(() => qk.products.list(filters.value)),
    queryFn: () => productsApi.list(filters.value),
    placeholderData: keepPreviousData,
    staleTime: 5 * 60_000,
  })
}

export function useProduct(id: Ref<string>) {
  return useQuery({
    queryKey: computed(() => qk.products.detail(id.value)),
    queryFn: () => productsApi.get(id.value),
    enabled: computed(() => !!id.value),
  })
}

export function useCreateProduct() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateProductCommand) => productsApi.create(body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.products.all })
    },
  })
}

export function useUpdateProduct(id: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateProductRequest) => productsApi.update(id, body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.products.all })
      void qc.invalidateQueries({ queryKey: qk.products.detail(id) })
    },
  })
}

export function useDeleteProduct() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => productsApi.remove(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.products.all })
    },
  })
}
