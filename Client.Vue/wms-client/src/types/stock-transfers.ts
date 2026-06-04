/** POST /api/stock-transfers body. */
export interface TransferStockRequest {
  productId: string
  sourceLocationId: string
  destinationLocationId: string
  lotId: string | null
  quantity: number
}
