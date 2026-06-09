import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { productCategoriesApi } from '@/api/endpoints/product-categories'
import { qk } from '@/api/query-keys'
import type {
  CreateProductCategoryCommand,
  UpdateProductCategoryRequest,
} from '@/types/categories'

export function useCategoryTree() {
  return useQuery({
    queryKey: qk.productCategories.tree(),
    queryFn: () => productCategoriesApi.tree(),
    staleTime: 5 * 60_000,
  })
}

/**
 * Flat category list for pickers / id→category lookups. Builds hierarchical
 * "Parent › Child" labels so a flat <Select> still conveys the tree shape.
 */
export function useCategoryOptions() {
  const query = useQuery({
    queryKey: qk.productCategories.list(),
    queryFn: () => productCategoriesApi.list(),
    staleTime: 10 * 60_000,
  })

  const byId = computed(
    () => new Map((query.data.value ?? []).map((c) => [c.id, c])),
  )

  const options = computed(() => {
    const map = byId.value
    const path = (id: string): string => {
      const node = map.get(id)
      if (!node) return ''
      return node.parentId ? `${path(node.parentId)} › ${node.name}` : node.name
    }
    return (query.data.value ?? [])
      .map((c) => ({ label: path(c.id), value: c.id }))
      .sort((a, b) => a.label.localeCompare(b.label))
  })

  return { ...query, options, byId }
}

function useInvalidateCategories() {
  const qc = useQueryClient()
  return () => {
    void qc.invalidateQueries({ queryKey: qk.productCategories.all })
    // Category changes ripple into product category names/counts shown elsewhere.
    void qc.invalidateQueries({ queryKey: qk.products.all })
  }
}

export function useCreateCategory() {
  const invalidate = useInvalidateCategories()
  return useMutation({
    mutationFn: (body: CreateProductCategoryCommand) => productCategoriesApi.create(body),
    onSuccess: invalidate,
  })
}

export function useUpdateCategory() {
  const invalidate = useInvalidateCategories()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateProductCategoryRequest }) =>
      productCategoriesApi.update(id, body),
    onSuccess: invalidate,
  })
}

export function useDeleteCategory() {
  const invalidate = useInvalidateCategories()
  return useMutation({
    mutationFn: (id: string) => productCategoriesApi.remove(id),
    onSuccess: invalidate,
  })
}
