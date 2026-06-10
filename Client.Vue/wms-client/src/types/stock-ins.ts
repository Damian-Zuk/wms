import type { LocationRef, LotRef, ProductRef } from './refs'
import type { PutawayStrategyType, StockInStatus } from './enums'

/** A planned/edited placement: where part of a line's quantity goes. */
export interface StockInPlacementDto {
  id: string
  location: LocationRef
  quantity: number
  placedQuantity: number
  strategy: PutawayStrategyType
}

/** A requested receipt line; its quantity is split across one or more placements. */
export interface StockInLineDto {
  id: string
  product: ProductRef
  lot: LotRef | null
  quantity: number
  placements: StockInPlacementDto[]
}

export interface StockInDto {
  id: string
  status: StockInStatus
  cancelledFrom: StockInStatus | null
  description: string | null
  createdAt: string
  createdBy: string | null
  modifiedBy: string | null
  modifiedAt: string | null
  lines: StockInLineDto[]
}

export interface StockInFilters {
  page: number
  pageSize: number
}

/** One create line — the caller does NOT pick locations; the planner does. */
export interface CreateStockInLine {
  productId: string
  lotId: string | null
  quantity: number
}

/** POST /api/stock-ins body. */
export interface CreateStockInCommand {
  lines: CreateStockInLine[]
  description: string | null
}

/** PATCH /api/stock-ins/{id}/description body. */
export interface UpdateStockInDescriptionRequest {
  description: string | null
}

/** One placement when manually re-planning a line. */
export interface ModifyPlacement {
  locationId: string
  quantity: number
}

/** PUT /api/stock-ins/{id}/lines/{lineId}/placements body. */
export interface ModifyLinePlacementsRequest {
  placements: ModifyPlacement[]
}
