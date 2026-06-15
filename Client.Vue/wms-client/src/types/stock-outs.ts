import type { HandlingUnitRef, LocationRef, LotRef, ProductRef } from './refs'
import type { PickingStrategyType, StockOutStatus } from './enums'

/** A single planned pick: a quantity drawn from one location (and lot, if tracked). */
export interface StockOutItemDto {
  id: string
  location: LocationRef
  lot: LotRef | null
  quantity: number
  pickedQuantity: number
  strategy: PickingStrategyType
  /** The handling unit the pick draws from; null = loose stock. */
  handlingUnit: HandlingUnitRef | null
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
  description: string | null
  createdAt: string
  createdBy: string | null
  lines: StockOutLineDto[]
}

export interface StockOutFilters {
  search?: string
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
  description: string | null
}

/** PATCH /api/stock-outs/{id}/description body. */
export interface UpdateStockOutDescriptionRequest {
  description: string | null
}

/** One hand-picked allocation when manually re-planning a line's picks. */
export interface PickAllocationInput {
  locationId: string
  lotId: string | null
  quantity: number
  /** Pin the pick to a handling unit at the location; null = loose stock. */
  handlingUnitId: string | null
}

/** PUT /api/stock-outs/{id}/lines/{lineId}/pick-locations body. */
export interface ModifyPickLocationsRequest {
  allocations: PickAllocationInput[]
}

/** POST /api/stock-outs/{id}/lines/{lineId}/replan body. */
export interface ReplanLineRequest {
  strategy: PickingStrategyType
}
