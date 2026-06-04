import { http } from '../http'
import type { TransferStockRequest } from '@/types/stock-transfers'

export const stockTransfersApi = {
  create: (body: TransferStockRequest) =>
    http.post<string>('/stock-transfers', body).then((r) => r.data),
}
