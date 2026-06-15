import { http } from '../http'
import type { PagedResult } from '@/types/common'
import type { StockMovementDto, StockMovementFilters } from '@/types/stock-movements'

export const stockMovementsApi = {
  list: (filters: StockMovementFilters) =>
    http
      .get<PagedResult<StockMovementDto>>('/stock-movements', {
        params: {
          page: filters.page,
          pageSize: filters.pageSize,
          productId: filters.productId,
          locationId: filters.locationId,
          lotId: filters.lotId,
          type: filters.type,
          source: filters.source,
          handlingUnitId: filters.handlingUnitId,
        },
      })
      .then((r) => r.data),

  get: (id: string) =>
    http.get<StockMovementDto>(`/stock-movements/${id}`).then((r) => r.data),
}
