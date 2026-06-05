import { http } from '../http'
import type { PagedResult } from '@/types/common'
import type {
  CreateProductCommand,
  ProductDto,
  ProductFilters,
  UpdateProductRequest,
} from '@/types/products'

export const productsApi = {
  list: (filters: ProductFilters) =>
    http
      .get<PagedResult<ProductDto>>('/products', {
        params: {
          search: filters.search || undefined,
          sortBy: filters.sortBy || undefined,
          sortDescending: filters.sortDescending || undefined,
          page: filters.page,
          pageSize: filters.pageSize,
        },
      })
      .then((r) => r.data),

  get: (id: string) => http.get<ProductDto>(`/products/${id}`).then((r) => r.data),

  create: (body: CreateProductCommand) =>
    http.post<string>('/products', body).then((r) => r.data),

  update: (id: string, body: UpdateProductRequest) =>
    http.put<void>(`/products/${id}`, body).then(() => undefined),

  remove: (id: string) => http.delete<void>(`/products/${id}`).then(() => undefined),
}
