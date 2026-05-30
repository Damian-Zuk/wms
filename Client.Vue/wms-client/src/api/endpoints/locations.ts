import { http } from '../http'
import type { PagedResult } from '@/types/common'
import type { LocationDto, LocationFilters } from '@/types/locations'

export const locationsApi = {
  list: (filters: LocationFilters) =>
    http
      .get<PagedResult<LocationDto>>('/locations', {
        params: {
          search: filters.search || undefined,
          zone: filters.zone || undefined,
          type: filters.type || undefined,
          page: filters.page,
          pageSize: filters.pageSize,
        },
      })
      .then((r) => r.data),
}
