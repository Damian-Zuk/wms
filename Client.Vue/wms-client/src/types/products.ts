import type { TemperatureZone } from './enums'

export interface ProductDto {
  id: string
  sku: string
  name: string
  description: string
  requiredTemperatureZone: TemperatureZone
  preferredLocationIds: string[]
}

export interface ProductFilters {
  search?: string
  page: number
  pageSize: number
}
