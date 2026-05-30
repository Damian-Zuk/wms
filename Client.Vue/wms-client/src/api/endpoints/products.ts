import { http } from '../http'
import type { PagedResult } from '@/types/common'
import type { ProductDto, ProductFilters } from '@/types/products'

export const productsApi = {
  list: (filters: ProductFilters) =>
    http
      .get<PagedResult<ProductDto>>('/products', {
        params: {
          search: filters.search || undefined,
          page: filters.page,
          pageSize: filters.pageSize,
        },
      })
      .then((r) => r.data),

  get: (id: string) => http.get<ProductDto>(`/products/${id}`).then((r) => r.data),
}
