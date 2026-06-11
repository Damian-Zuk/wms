import { http } from '../http'
import type { PagedResult } from '@/types/common'
import type {
  CreateStockInCommand,
  ModifyLinePlacementsRequest,
  StockInDto,
  StockInFilters,
  UpdateStockInDescriptionRequest,
} from '@/types/stock-ins'

export const stockInsApi = {
  list: (filters: StockInFilters) =>
    http
      .get<PagedResult<StockInDto>>('/stock-ins', {
        params: { search: filters.search || undefined, page: filters.page, pageSize: filters.pageSize },
      })
      .then((r) => r.data),

  get: (id: string) => http.get<StockInDto>(`/stock-ins/${id}`).then((r) => r.data),

  create: (body: CreateStockInCommand) =>
    http.post<string>('/stock-ins', body).then((r) => r.data),

  // Update the description (any status, Admin/Manager).
  updateDescription: (id: string, body: UpdateStockInDescriptionRequest) =>
    http.patch<void>(`/stock-ins/${id}/description`, body).then(() => undefined),

  // Re-plan a single line's placements (Draft only, Admin/Manager).
  modifyLinePlacements: (
    stockInId: string,
    lineId: string,
    body: ModifyLinePlacementsRequest,
  ) =>
    http
      .put<void>(`/stock-ins/${stockInId}/lines/${lineId}/placements`, body)
      .then(() => undefined),

  // Re-run the putaway planner for a single line (Draft only, Admin/Manager).
  replanLine: (stockInId: string, lineId: string) =>
    http
      .post<void>(`/stock-ins/${stockInId}/lines/${lineId}/replan`)
      .then(() => undefined),

  // Put away a single placement (whole or partial); books stock immediately.
  putawayItem: (stockInId: string, itemId: string, body: { quantity: number }) =>
    http
      .post<void>(`/stock-ins/${stockInId}/items/${itemId}/putaway`, body)
      .then(() => undefined),

  // Workflow transitions — bodyless POSTs returning 204.
  startPutaway: (id: string) =>
    http.post<void>(`/stock-ins/${id}/start-putaway`).then(() => undefined),

  complete: (id: string) =>
    http.post<void>(`/stock-ins/${id}/complete`).then(() => undefined),

  cancel: (id: string) =>
    http.post<void>(`/stock-ins/${id}/cancel`).then(() => undefined),
}
