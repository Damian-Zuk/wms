<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import Column from 'primevue/column'
import Button from 'primevue/button'
import DataTableWrapper from '@/components/common/DataTableWrapper.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import { useStockIns } from './useStockIns'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/lib/date'
import { stockInStatusSeverity } from '@/lib/enum-display'
import type { StockInDto, StockInFilters } from '@/types/stock-ins'

const router = useRouter()
const auth = useAuthStore()

const filters = ref<StockInFilters>({ page: 1, pageSize: 20 })
const { data, isFetching } = useStockIns(filters)

function setPage(page: number) {
  filters.value = { ...filters.value, page }
}

function setPageSize(pageSize: number) {
  filters.value = { ...filters.value, pageSize, page: 1 }
}

function openStockIn(stockIn: StockInDto) {
  router.push({ name: 'stock-in-detail', params: { id: stockIn.id } })
}
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1400px">
    <div class="flex items-center justify-between gap-4">
      <h1 class="text-2xl font-semibold text-surface-900">Stock-Ins</h1>
      <Button
        v-if="auth.canMutate"
        label="New Stock-In"
        icon="pi pi-plus"
        @click="router.push({ name: 'stock-in-create' })"
      />
    </div>

    <DataTableWrapper
      :items="data?.items ?? []"
      :total-count="data?.totalCount ?? 0"
      :page="filters.page"
      :page-size="filters.pageSize"
      :loading="isFetching"
      class="cursor-pointer"
      @update:page="setPage"
      @update:page-size="setPageSize"
      @row-click="openStockIn"
    >
      <Column header="Created" style="width: 16rem">
        <template #body="{ data: row }: { data: StockInDto }">
          {{ formatDateTime(row.createdAt) }}
        </template>
      </Column>
      <Column header="Created By">
        <template #body="{ data: row }: { data: StockInDto }">
          {{ row.createdBy ?? '—' }}
        </template>
      </Column>
      <Column header="Lines" style="width: 8rem">
        <template #body="{ data: row }: { data: StockInDto }">
          {{ row.lines.length }}
        </template>
      </Column>
      <Column header="Status" style="width: 11rem">
        <template #body="{ data: row }: { data: StockInDto }">
          <StatusBadge :value="row.status" :severity="stockInStatusSeverity[row.status]" />
        </template>
      </Column>
    </DataTableWrapper>
  </section>
</template>
