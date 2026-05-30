import type { LocationRef, LotRef, ProductRef } from './refs'
import type { StockInStatus } from './enums'

export interface StockInItemDto {
  id: string
  product: ProductRef
  location: LocationRef
  lot: LotRef | null
  quantity: number
}

export interface StockInDto {
  id: string
  status: StockInStatus
  createdAt: string
  createdBy: string | null
  items: StockInItemDto[]
}

export interface StockInFilters {
  page: number
  pageSize: number
}

export interface CreateStockInItem {
  productId: string
  locationId: string
  lotId: string | null
  quantity: number
}

/** POST /api/stock-ins body. */
export interface CreateStockInCommand {
  items: CreateStockInItem[]
}
