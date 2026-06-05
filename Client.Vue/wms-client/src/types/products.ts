import type { TemperatureZone } from './enums'

export interface ProductDto {
  id: string
  sku: string
  name: string
  description: string
  /** Weight of one unit in kilograms. */
  weight: number
  /** Volume of one unit in cubic decimetres (litres). */
  volume: number
  requiredTemperatureZone: TemperatureZone
  preferredLocationIds: string[]
}

export interface ProductFilters {
  search?: string
  page: number
  pageSize: number
}

export interface ProductFormValues {
  sku: string
  name: string
  description: string
  weight: number
  volume: number
  requiredTemperatureZone: TemperatureZone
  preferredLocationIds: string[]
}

/** POST /api/products body (SKU is set on creation). */
export type CreateProductCommand = ProductFormValues

/** PUT /api/products/{id} body (SKU is immutable). */
export interface UpdateProductRequest {
  name: string
  description: string
  weight: number
  volume: number
  requiredTemperatureZone: TemperatureZone
  preferredLocationIds: string[]
}
