import { http } from '../http'
import type { PagedResult } from '@/types/common'
import type {
  AdjustInventoryRequest,
  AvailabilityDto,
  AvailabilityParams,
  InventoryDto,
  InventoryFilters,
} from '@/types/inventory'

export const inventoryApi = {
  list: (filters: InventoryFilters) =>
    http
      .get<PagedResult<InventoryDto>>('/inventories', {
        params: {
          productId: filters.productId || undefined,
          locationId: filters.locationId || undefined,
          lotId: filters.lotId || undefined,
          expiringWithinDays: filters.expiringWithinDays ?? undefined,
          sortBy: filters.sortBy || undefined,
          sortDescending: filters.sortDescending || undefined,
          page: filters.page,
          pageSize: filters.pageSize,
        },
      })
      .then((r) => r.data),

  get: (id: string) => http.get<InventoryDto>(`/inventories/${id}`).then((r) => r.data),

  adjust: (id: string, body: AdjustInventoryRequest) =>
    http.post<void>(`/inventories/${id}/adjust`, body).then(() => undefined),

  availability: (params: AvailabilityParams) =>
    http
      .get<AvailabilityDto>('/inventories/availability', {
        params: {
          productId: params.productId,
          locationId: params.locationId || undefined,
          lotId: params.lotId || undefined,
        },
      })
      .then((r) => r.data),
}
