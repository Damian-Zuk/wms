import type { ProductFilters } from '@/types/products'
import type { LocationFilters } from '@/types/locations'
import type { LotFilters } from '@/types/lots'
import type { AvailabilityParams, InventoryFilters } from '@/types/inventory'
import type { StockInFilters } from '@/types/stock-ins'
import type { StockOutFilters } from '@/types/stock-outs'
import type { StockMovementFilters } from '@/types/stock-movements'

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
    options: (productId?: string) => ['lots', 'options', productId ?? null] as const,
  },
  locations: {
    all: ['locations'] as const,
    options: () => ['locations', 'options'] as const,
    list: (filters: LocationFilters) => ['locations', 'list', filters] as const,
    detail: (id: string) => ['locations', 'detail', id] as const,
  },
  inventories: {
    all: ['inventories'] as const,
    list: (filters: InventoryFilters) => ['inventories', 'list', filters] as const,
    detail: (id: string) => ['inventories', 'detail', id] as const,
    availability: (params: AvailabilityParams) =>
      ['inventories', 'availability', params] as const,
  },
  stockIns: {
    all: ['stock-ins'] as const,
    list: (filters: StockInFilters) => ['stock-ins', 'list', filters] as const,
    detail: (id: string) => ['stock-ins', 'detail', id] as const,
  },
  stockOuts: {
    all: ['stock-outs'] as const,
    list: (filters: StockOutFilters) => ['stock-outs', 'list', filters] as const,
    detail: (id: string) => ['stock-outs', 'detail', id] as const,
  },
  stockMovements: {
    all: ['stock-movements'] as const,
    list: (filters: StockMovementFilters) =>
      ['stock-movements', 'list', filters] as const,
    detail: (id: string) => ['stock-movements', 'detail', id] as const,
  },
  users: {
    all: ['users'] as const,
    list: () => ['users', 'list'] as const,
  },
}
