import type { LocationRef, LotRef, ProductRef } from './refs'
import type { PickingStrategyType, StockOutStatus } from './enums'

/** A single planned pick: a quantity drawn from one location (and lot, if tracked). */
export interface StockOutItemDto {
  id: string
  location: LocationRef
  lot: LotRef | null
  quantity: number
  pickedQuantity: number
  strategy: PickingStrategyType
}

/** A requested line; its quantity is split across one or more picks by the planner. */
export interface StockOutLineDto {
  id: string
  product: ProductRef
  strategy: PickingStrategyType
  quantity: number
  items: StockOutItemDto[]
}

export interface StockOutDto {
  id: string
  status: StockOutStatus
  cancelledFrom: StockOutStatus | null
  createdAt: string
  createdBy: string | null
  lines: StockOutLineDto[]
}

export interface StockOutFilters {
  page: number
  pageSize: number
}

/** One create line — the caller chooses a product, a strategy and a quantity; the planner picks the locations/lots. */
export interface CreateStockOutLine {
  productId: string
  strategy: PickingStrategyType
  quantity: number
}

/** POST /api/stock-outs body. */
export interface CreateStockOutCommand {
  lines: CreateStockOutLine[]
}
