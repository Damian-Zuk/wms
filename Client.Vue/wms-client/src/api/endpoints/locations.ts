import { http } from '../http'
import type { PagedResult } from '@/types/common'
import type {
  CreateLocationCommand,
  LocationDto,
  LocationFilters,
  UpdateLocationRequest,
} from '@/types/locations'

export const locationsApi = {
  list: (filters: LocationFilters) =>
    http
      .get<PagedResult<LocationDto>>('/locations', {
        params: {
          search: filters.search || undefined,
          zone: filters.zone || undefined,
          type: filters.type || undefined,
          temperatureZone: filters.temperatureZone || undefined,
          sortBy: filters.sortBy || undefined,
          sortDescending: filters.sortDescending || undefined,
          page: filters.page,
          pageSize: filters.pageSize,
        },
      })
      .then((r) => r.data),

  get: (id: string) => http.get<LocationDto>(`/locations/${id}`).then((r) => r.data),

  create: (body: CreateLocationCommand) =>
    http.post<string>('/locations', body).then((r) => r.data),

  update: (id: string, body: UpdateLocationRequest) =>
    http.put<void>(`/locations/${id}`, body).then(() => undefined),

  setPreferredProducts: (id: string, productIds: string[]) =>
    http
      .put<void>(`/locations/${id}/preferred-products`, { productIds })
      .then(() => undefined),

  remove: (id: string) => http.delete<void>(`/locations/${id}`).then(() => undefined),
}
