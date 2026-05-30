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
  page: number
  pageSize: number
}
