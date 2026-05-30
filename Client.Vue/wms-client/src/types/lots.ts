export interface LotDto {
  id: string
  number: string
  productId: string
  /** ISO date "yyyy-MM-dd" or null */
  manufactureDate: string | null
  expirationDate: string | null
  isExpired: boolean
  isExpiringSoon: boolean
}

export interface LotFilters {
  productId?: string
  search?: string
  page: number
  pageSize: number
}

/** Form holds Date objects; converted to "yyyy-MM-dd" on submit. */
export interface LotFormValues {
  number: string
  productId: string
  manufactureDate: Date | null
  expirationDate: Date | null
}

/** POST /api/lots body. */
export interface CreateLotCommand {
  number: string
  productId: string
  manufactureDate: string | null
  expirationDate: string | null
}

/** PUT /api/lots/{id} body (only dates are mutable). */
export interface UpdateLotRequest {
  manufactureDate: string | null
  expirationDate: string | null
}
