import type { InjectionKey, Ref } from 'vue'
import type { CategoryTreeNode } from '@/types/categories'

/**
 * Shared handlers for the recursive category tree, passed down via provide/inject
 * so each node can act without bubbling events through every level of recursion.
 */
export interface CategoryTreeContext {
  canMutate: boolean
  /** Id of the node currently being dragged, or null. */
  draggingId: Ref<string | null>
  /** Id of the node currently hovered as a drop target, or null. */
  dragOverId: Ref<string | null>
  isCollapsed: (id: string) => boolean
  toggle: (id: string) => void
  addChild: (parentId: string) => void
  rename: (node: CategoryTreeNode) => void
  remove: (node: CategoryTreeNode) => void
  view: (node: CategoryTreeNode) => void
  /** Re-parent dragId under dropId (null = make it a root). */
  move: (dragId: string, dropId: string | null) => void
}

export const categoryTreeKey: InjectionKey<CategoryTreeContext> = Symbol('categoryTree')
