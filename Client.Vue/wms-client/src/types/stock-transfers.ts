/** POST /api/stock-transfers body. */
export interface TransferStockRequest {
  productId: string
  sourceLocationId: string
  destinationLocationId: string
  lotId: string | null
  quantity: number
  /** Draw from this handling unit at the source; null = loose stock. */
  sourceHandlingUnitId: string | null
  /** Land on this handling unit at the destination; null = loose stock. */
  destinationHandlingUnitId: string | null
}
