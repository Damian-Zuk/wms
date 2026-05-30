<script setup lang="ts" generic="T">
import DataTable from 'primevue/datatable'

withDefaults(
  defineProps<{
    items: T[]
    totalCount?: number
    page?: number
    pageSize?: number
    loading?: boolean
    dataKey?: string
    rowsPerPageOptions?: number[]
  }>(),
  {
    totalCount: 0,
    page: 1,
    pageSize: 20,
    loading: false,
    dataKey: 'id',
    rowsPerPageOptions: () => [10, 20, 50],
  },
)

const emit = defineEmits<{
  'update:page': [page: number]
  'update:pageSize': [pageSize: number]
}>()

// PrimeVue's @page emits { first, rows, page (0-based), pageCount }.
function onPage(event: { page: number; rows: number; first: number }) {
  emit('update:pageSize', event.rows)
  emit('update:page', event.page + 1)
}
</script>

<template>
  <DataTable
    :value="items"
    :data-key="dataKey"
    :loading="loading"
    lazy
    paginator
    :rows="pageSize"
    :first="(page - 1) * pageSize"
    :total-records="totalCount"
    :rows-per-page-options="rowsPerPageOptions"
    paginator-template="FirstPageLink PrevPageLink CurrentPageReport NextPageLink LastPageLink RowsPerPageDropdown"
    current-page-report-template="{first}–{last} of {totalRecords}"
    @page="onPage"
  >
    <slot />

    <template #empty>
      <div class="p-4 text-center text-surface-500">No records found.</div>
    </template>
  </DataTable>
</template>
