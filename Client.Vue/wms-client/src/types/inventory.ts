import type { LocationRef, LotRef, ProductRef } from './refs'

export interface InventoryDto {
  id: string
  product: ProductRef
  location: LocationRef
  lot: LotRef | null
  onHand: number
  reserved: number
  available: number
}

export interface InventoryFilters {
  productId?: string
  locationId?: string
  lotId?: string
  page: number
  pageSize: number
}

/** POST /api/inventories/{id}/adjust body. */
export interface AdjustInventoryRequest {
  quantityChange: number
  reason: string | null
}

export interface AvailabilityParams {
  productId: string
  locationId?: string
  lotId?: string
}

export interface AvailabilityDto {
  productId: string
  productSku: string
  productName: string
  locationId: string | null
  lotId: string | null
  onHand: number
  reserved: number
  available: number
}

export interface ExpiringInventoryLineDto {
  inventoryId: string
  productId: string
  productSku: string
  lotId: string
  lotNumber: string
  expirationDate: string
  locationId: string
  locationCode: string
  available: number
}
