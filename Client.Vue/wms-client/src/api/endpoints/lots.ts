import { http } from '../http'
import type { PagedResult } from '@/types/common'
import type {
  CreateLotCommand,
  LotDto,
  LotFilters,
  UpdateLotRequest,
} from '@/types/lots'

export const lotsApi = {
  list: (filters: LotFilters) =>
    http
      .get<PagedResult<LotDto>>('/lots', {
        params: {
          productId: filters.productId || undefined,
          search: filters.search || undefined,
          page: filters.page,
          pageSize: filters.pageSize,
        },
      })
      .then((r) => r.data),

  get: (id: string) => http.get<LotDto>(`/lots/${id}`).then((r) => r.data),

  create: (body: CreateLotCommand) =>
    http.post<string>('/lots', body).then((r) => r.data),

  update: (id: string, body: UpdateLotRequest) =>
    http.put<void>(`/lots/${id}`, body).then(() => undefined),

  remove: (id: string) => http.delete<void>(`/lots/${id}`).then(() => undefined),
}
