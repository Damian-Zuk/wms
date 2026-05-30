import type { ProductFilters } from '@/types/products'

export const qk = {
  products: {
    all: ['products'] as const,
    list: (filters: ProductFilters) => ['products', 'list', filters] as const,
    detail: (id: string) => ['products', 'detail', id] as const,
  },
  locations: {
    all: ['locations'] as const,
    options: () => ['locations', 'options'] as const,
  },
}
