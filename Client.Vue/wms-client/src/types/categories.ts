export interface ProductCategoryDto {
  id: string
  name: string
  parentId: string | null
}

/** Node in the category tree returned by GET /api/product-categories/tree. */
export interface CategoryTreeNode {
  id: string
  name: string
  parentId: string | null
  /** Products assigned directly to this category. */
  directSkuCount: number
  /** Products in this category and all of its descendants. */
  totalSkuCount: number
  children: CategoryTreeNode[]
}

/** POST /api/product-categories body. */
export interface CreateProductCategoryCommand {
  name: string
  parentId: string | null
}

/** PUT /api/product-categories/{id} body (handles rename and re-parent). */
export interface UpdateProductCategoryRequest {
  name: string
  parentId: string | null
}
