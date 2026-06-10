import { http } from '../http'
import type { PagedResult } from '@/types/common'
import type {
  CreateStockOutCommand,
  ModifyPickLocationsRequest,
  ReplanLineRequest,
  StockOutDto,
  StockOutFilters,
  UpdateStockOutDescriptionRequest,
} from '@/types/stock-outs'

export const stockOutsApi = {
  list: (filters: StockOutFilters) =>
    http
      .get<PagedResult<StockOutDto>>('/stock-outs', {
        params: { page: filters.page, pageSize: filters.pageSize },
      })
      .then((r) => r.data),

  get: (id: string) => http.get<StockOutDto>(`/stock-outs/${id}`).then((r) => r.data),

  create: (body: CreateStockOutCommand) =>
    http.post<string>('/stock-outs', body).then((r) => r.data),

  // Update the description (any status, Admin/Manager).
  updateDescription: (id: string, body: UpdateStockOutDescriptionRequest) =>
    http.patch<void>(`/stock-outs/${id}/description`, body).then(() => undefined),

  // Re-plan a single line's pick allocations (Draft only, Admin/Manager).
  modifyPickLocations: (
    stockOutId: string,
    lineId: string,
    body: ModifyPickLocationsRequest,
  ) =>
    http
      .put<void>(`/stock-outs/${stockOutId}/lines/${lineId}/pick-locations`, body)
      .then(() => undefined),

  // Re-run the picking planner for a single line with a chosen strategy (Draft only, Admin/Manager).
  replanLine: (stockOutId: string, lineId: string, body: ReplanLineRequest) =>
    http
      .post<void>(`/stock-outs/${stockOutId}/lines/${lineId}/replan`, body)
      .then(() => undefined),

  // Pick a single item (whole or partial); removes stock immediately.
  pickItem: (stockOutId: string, itemId: string, body: { quantity: number }) =>
    http
      .post<void>(`/stock-outs/${stockOutId}/items/${itemId}/pick`, body)
      .then(() => undefined),

  // Workflow transitions — bodyless POSTs returning 204.
  startPicking: (id: string) =>
    http.post<void>(`/stock-outs/${id}/start-picking`).then(() => undefined),

  complete: (id: string) =>
    http.post<void>(`/stock-outs/${id}/complete`).then(() => undefined),

  cancel: (id: string) =>
    http.post<void>(`/stock-outs/${id}/cancel`).then(() => undefined),
}
