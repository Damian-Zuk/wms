import type { ProductFilters } from '@/types/products'
import type { LocationFilters } from '@/types/locations'
import type { LotFilters } from '@/types/lots'

export const qk = {
  products: {
    all: ['products'] as const,
    list: (filters: ProductFilters) => ['products', 'list', filters] as const,
    detail: (id: string) => ['products', 'detail', id] as const,
    options: () => ['products', 'options'] as const,
  },
  lots: {
    all: ['lots'] as const,
    list: (filters: LotFilters) => ['lots', 'list', filters] as const,
    detail: (id: string) => ['lots', 'detail', id] as const,
  },
  locations: {
    all: ['locations'] as const,
    options: () => ['locations', 'options'] as const,
    list: (filters: LocationFilters) => ['locations', 'list', filters] as const,
    detail: (id: string) => ['locations', 'detail', id] as const,
  },
}
