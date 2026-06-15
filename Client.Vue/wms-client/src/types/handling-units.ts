import type { HandlingUnitType } from './enums'
import type { LocationRef, LotRef, ProductRef } from './refs'

export interface HandlingUnitContentDto {
  inventoryId: string
  product: ProductRef
  lot: LotRef | null
  onHand: number
  reserved: number
  available: number
  receivedAt: string | null
}

export interface HandlingUnitDto {
  id: string
  code: string
  type: HandlingUnitType
  location: LocationRef | null
  createdAt: string
  createdBy: string | null
  contents: HandlingUnitContentDto[]
}

export interface HandlingUnitListItemDto {
  id: string
  code: string
  type: HandlingUnitType
  location: LocationRef | null
  totalOnHand: number
  productCount: number
  createdAt: string
}

export interface HandlingUnitFilters {
  search?: string
  locationId?: string
  type?: HandlingUnitType
  isEmpty?: boolean
  sortBy?: string
  sortDescending?: boolean
  page: number
  pageSize: number
}

/** POST /api/handling-units body. */
export interface CreateHandlingUnitCommand {
  locationId: string
  type: HandlingUnitType
  code: string | null
}

/** POST /api/handling-units/{id}/move body. */
export interface MoveHandlingUnitRequest {
  destinationLocationId: string
}

/** POST /api/handling-units/{id}/pack and /unpack body. */
export interface PackHandlingUnitRequest {
  productId: string
  lotId: string | null
  quantity: number
}
