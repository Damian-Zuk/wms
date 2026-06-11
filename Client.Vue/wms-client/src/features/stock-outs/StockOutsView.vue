<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import Column from 'primevue/column'
import Button from 'primevue/button'
import DataTableWrapper from '@/components/common/DataTableWrapper.vue'
import ListingHeader from '@/components/common/ListingHeader.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import { useStockOuts } from './useStockOuts'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/lib/date'
import { formatCurrency } from '@/lib/money'
import { stockOutStatusSeverity } from '@/lib/enum-display'
import type { StockOutDto, StockOutFilters } from '@/types/stock-outs'

const router = useRouter()
const auth = useAuthStore()

const filters = ref<StockOutFilters>({ page: 1, pageSize: 20 })
const { data, isFetching, refetch } = useStockOuts(filters)

function setPage(page: number) {
  filters.value = { ...filters.value, page }
}

function setPageSize(pageSize: number) {
  filters.value = { ...filters.value, pageSize, page: 1 }
}

function openStockOut(stockOut: StockOutDto) {
  router.push({ name: 'stock-out-detail', params: { id: stockOut.id } })
}
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1600px">
    <div class="flex items-center justify-between gap-4">
      <ListingHeader title="Stock-Outs" :count="data?.totalCount ?? 0" :loading="isFetching" @refresh="refetch" />
      <Button
        v-if="auth.canMutate"
        label="New Stock-Out"
        icon="pi pi-plus"
        @click="router.push({ name: 'stock-out-create' })"
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
      @row-click="openStockOut"
    >
      <Column header="Created" style="width: 16rem">
        <template #body="{ data: row }: { data: StockOutDto }">
          {{ formatDateTime(row.createdAt) }}
        </template>
      </Column>
      <Column header="Created By">
        <template #body="{ data: row }: { data: StockOutDto }">
          {{ row.createdBy ?? '—' }}
        </template>
      </Column>
      <Column header="Description">
        <template #body="{ data: row }: { data: StockOutDto }">
          <span class="text-surface-600">
            {{ row.description ? row.description.slice(0, 100) + (row.description.length > 100 ? '…' : '') : '—' }}
          </span>
        </template>
      </Column>
      <Column header="Lines" style="width: 8rem">
        <template #body="{ data: row }: { data: StockOutDto }">
          {{ row.lines.length }}
        </template>
      </Column>
      <Column header="Value" style="width: 10rem">
        <template #body="{ data: row }: { data: StockOutDto }">
          {{ formatCurrency(row.lines.reduce((sum, l) => sum + l.quantity * l.product.unitPrice, 0)) }}
        </template>
      </Column>
      <Column header="Status" style="width: 11rem">
        <template #body="{ data: row }: { data: StockOutDto }">
          <StatusBadge :value="row.status" :severity="stockOutStatusSeverity[row.status]" />
        </template>
      </Column>
    </DataTableWrapper>
  </section>
</template>
