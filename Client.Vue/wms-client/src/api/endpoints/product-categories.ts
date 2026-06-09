import { http } from '../http'
import type {
  CategoryTreeNode,
  CreateProductCategoryCommand,
  ProductCategoryDto,
  UpdateProductCategoryRequest,
} from '@/types/categories'

export const productCategoriesApi = {
  tree: () =>
    http.get<CategoryTreeNode[]>('/product-categories/tree').then((r) => r.data),

  list: () =>
    http.get<ProductCategoryDto[]>('/product-categories').then((r) => r.data),

  create: (body: CreateProductCategoryCommand) =>
    http.post<string>('/product-categories', body).then((r) => r.data),

  update: (id: string, body: UpdateProductCategoryRequest) =>
    http.put<void>(`/product-categories/${id}`, body).then(() => undefined),

  remove: (id: string) =>
    http.delete<void>(`/product-categories/${id}`).then(() => undefined),
}
