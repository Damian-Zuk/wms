/** Sort fields shared by every paged list filter. */
export interface SortableFilters {
  sortBy?: string
  sortDescending?: boolean
}

/** The payload emitted by DataTableWrapper's `update:sort` event. */
export interface SortChange {
  field: string | null
  order: number | null
}

/**
 * Maps a DataTable sort event onto the `sortBy` / `sortDescending` filter
 * fields. A cleared sort (field null) resets to the backend's default order.
 */
export function toSortFilters(change: SortChange): SortableFilters {
  return {
    sortBy: change.field ?? undefined,
    sortDescending: change.order === -1,
  }
}

/** Current sort order in PrimeVue terms (1 asc, -1 desc, null unsorted). */
export function sortOrderOf(filters: SortableFilters): number | null {
  if (!filters.sortBy) return null
  return filters.sortDescending ? -1 : 1
}
