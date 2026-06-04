import type { LocationRef, LotRef, ProductRef } from './refs'
import type { StockMovementSource, StockMovementType } from './enums'

export interface StockMovementDto {
  id: string
  product: ProductRef
  location: LocationRef
  lot: LotRef | null
  quantityChange: number
  type: StockMovementType
  source: StockMovementSource
  sourceId: string
  createdAt: string
}

export interface StockMovementFilters {
  page: number
  pageSize: number
  productId?: string
  locationId?: string
  lotId?: string
  type?: StockMovementType
  source?: StockMovementSource
}
