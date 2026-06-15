import { http } from '../http'
import type { PagedResult } from '@/types/common'
import type {
  CreateHandlingUnitCommand,
  HandlingUnitDto,
  HandlingUnitFilters,
  HandlingUnitListItemDto,
  MoveHandlingUnitRequest,
  PackHandlingUnitRequest,
} from '@/types/handling-units'

export const handlingUnitsApi = {
  list: (filters: HandlingUnitFilters) =>
    http
      .get<PagedResult<HandlingUnitListItemDto>>('/handling-units', {
        params: {
          search: filters.search || undefined,
          locationId: filters.locationId || undefined,
          type: filters.type || undefined,
          isEmpty: filters.isEmpty ?? undefined,
          sortBy: filters.sortBy || undefined,
          sortDescending: filters.sortDescending || undefined,
          page: filters.page,
          pageSize: filters.pageSize,
        },
      })
      .then((r) => r.data),

  get: (id: string) =>
    http.get<HandlingUnitDto>(`/handling-units/${id}`).then((r) => r.data),

  create: (body: CreateHandlingUnitCommand) =>
    http.post<string>('/handling-units', body).then((r) => r.data),

  move: (id: string, body: MoveHandlingUnitRequest) =>
    http.post<void>(`/handling-units/${id}/move`, body).then(() => undefined),

  pack: (id: string, body: PackHandlingUnitRequest) =>
    http.post<void>(`/handling-units/${id}/pack`, body).then(() => undefined),

  unpack: (id: string, body: PackHandlingUnitRequest) =>
    http.post<void>(`/handling-units/${id}/unpack`, body).then(() => undefined),

  remove: (id: string) =>
    http.delete<void>(`/handling-units/${id}`).then(() => undefined),
}
