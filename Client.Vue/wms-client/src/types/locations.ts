import type { LocationType, TemperatureZone } from './enums'

export interface LocationAddressDto {
  zone: string
  aisle: string
  rack: string
  shelf: string
  bin: string
}

export interface LocationDto {
  id: string
  code: string
  address: LocationAddressDto
  display: string
  type: LocationType
  temperatureZone: TemperatureZone
  capacity: number | null
  /** Total physical units currently on hand across all inventory in this location. */
  occupancy: number
  /** Weight limit in kilograms; null = unlimited. */
  weightCapacity: number | null
  /** Total weight (kg) currently on hand across all inventory in this location. */
  weightOccupancy: number
  /** Volume limit in cubic decimetres; null = unlimited. */
  volumeCapacity: number | null
  /** Total volume (dm³) currently on hand across all inventory in this location. */
  volumeOccupancy: number
  isMixedSkuAllowed: boolean
  isMixedLotAllowed: boolean
  isActive: boolean
  isBlocked: boolean
  blockedReason: string | null
  description: string | null
}

export interface LocationFilters {
  search?: string
  zone?: string
  type?: LocationType
  sortBy?: string
  sortDescending?: boolean
  page: number
  pageSize: number
}

/** Shared shape for the create/edit form. */
export interface LocationFormValues {
  code: string
  zone: string
  aisle: string
  rack: string
  shelf: string
  bin: string
  type: LocationType
  temperatureZone: TemperatureZone
  capacity: number | null
  weightCapacity: number | null
  volumeCapacity: number | null
  description: string
  isMixedSkuAllowed: boolean
  isMixedLotAllowed: boolean
}

/** POST /api/locations body. */
export interface CreateLocationCommand {
  code: string
  zone: string
  aisle: string
  rack: string
  shelf: string
  bin: string
  type: LocationType
  description: string | null
  temperatureZone: TemperatureZone
  capacity: number | null
  weightCapacity: number | null
  volumeCapacity: number | null
  isMixedSkuAllowed: boolean
  isMixedLotAllowed: boolean
}

/** PUT /api/locations/{id} body (same fields as create). */
export type UpdateLocationRequest = CreateLocationCommand
