import { http } from '../http'
import type { PagedResult } from '@/types/common'
import type {
  CreateStockInCommand,
  ModifyLinePlacementsRequest,
  StockInDto,
  StockInFilters,
} from '@/types/stock-ins'

export const stockInsApi = {
  list: (filters: StockInFilters) =>
    http
      .get<PagedResult<StockInDto>>('/stock-ins', {
        params: { page: filters.page, pageSize: filters.pageSize },
      })
      .then((r) => r.data),

  get: (id: string) => http.get<StockInDto>(`/stock-ins/${id}`).then((r) => r.data),

  create: (body: CreateStockInCommand) =>
    http.post<string>('/stock-ins', body).then((r) => r.data),

  // Re-plan a single line's placements (Draft only, Admin/Manager).
  modifyLinePlacements: (
    stockInId: string,
    lineId: string,
    body: ModifyLinePlacementsRequest,
  ) =>
    http
      .put<void>(`/stock-ins/${stockInId}/lines/${lineId}/placements`, body)
      .then(() => undefined),

  // Workflow transitions — bodyless POSTs returning 204.
  startReceiving: (id: string) =>
    http.post<void>(`/stock-ins/${id}/start-receiving`).then(() => undefined),

  receive: (id: string) =>
    http.post<void>(`/stock-ins/${id}/receive`).then(() => undefined),

  complete: (id: string) =>
    http.post<void>(`/stock-ins/${id}/complete`).then(() => undefined),

  cancel: (id: string) =>
    http.post<void>(`/stock-ins/${id}/cancel`).then(() => undefined),
}
