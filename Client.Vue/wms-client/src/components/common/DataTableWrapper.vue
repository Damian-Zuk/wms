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
    paginate?: boolean
  }>(),
  {
    totalCount: 0,
    page: 1,
    pageSize: 20,
    loading: false,
    dataKey: 'id',
    rowsPerPageOptions: () => [10, 20, 50],
    paginate: true,
  },
)

const emit = defineEmits<{
  'update:page': [page: number]
  'update:pageSize': [pageSize: number]
  'row-click': [row: T]
}>()

function onPage(event: { page: number; rows: number; first: number }) {
  emit('update:pageSize', event.rows)
  emit('update:page', event.page + 1)
}

function onRowClick(event: { data: T }) {
  emit('row-click', event.data)
}
</script>

<template>
  <DataTable
    :value="items"
    :data-key="dataKey"
    :loading="loading"
    :lazy="paginate"
    :paginator="paginate"
    :rows="pageSize"
    :first="(page - 1) * pageSize"
    :total-records="totalCount"
    :rows-per-page-options="rowsPerPageOptions"
    paginator-template="FirstPageLink PrevPageLink CurrentPageReport NextPageLink LastPageLink RowsPerPageDropdown"
    current-page-report-template="{first}–{last} of {totalRecords}"
    :row-hover="true"
    @page="onPage"
    @row-click="onRowClick"
  >
    <slot />

    <template #empty>
      <div class="p-4 text-center text-surface-500">No records found.</div>
    </template>
  </DataTable>
</template>
