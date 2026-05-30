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

export interface ProductFormValues {
  sku: string
  name: string
  description: string
  requiredTemperatureZone: TemperatureZone
  preferredLocationIds: string[]
}

/** POST /api/products body (SKU is set on creation). */
export type CreateProductCommand = ProductFormValues

/** PUT /api/products/{id} body (SKU is immutable). */
export interface UpdateProductRequest {
  name: string
  description: string
  requiredTemperatureZone: TemperatureZone
  preferredLocationIds: string[]
}
