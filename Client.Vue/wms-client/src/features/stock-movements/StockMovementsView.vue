<script setup lang="ts">
import { ref } from 'vue'
import Column from 'primevue/column'
import Select from 'primevue/select'
import DataTableWrapper from '@/components/common/DataTableWrapper.vue'
import ListingHeader from '@/components/common/ListingHeader.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import ProductSelect from '@/components/pickers/ProductSelect.vue'
import LocationSelect from '@/components/pickers/LocationSelect.vue'
import { useStockMovements } from './useStockMovements'
import { formatDateTime } from '@/lib/date'
import {
  stockMovementSourceLabel,
  stockMovementSourceSeverity,
  stockMovementTypeSeverity,
} from '@/lib/enum-display'
import type { StockMovementFilters, StockMovementDto } from '@/types/stock-movements'
import type { StockMovementSource, StockMovementType } from '@/types/enums'

const filters = ref<StockMovementFilters>({ page: 1, pageSize: 20 })
const productFilter = ref<string | null>(null)
const locationFilter = ref<string | null>(null)
const typeFilter = ref<StockMovementType | null>(null)
const sourceFilter = ref<StockMovementSource | null>(null)

const { data, isFetching, refetch } = useStockMovements(filters)

const typeOptions: { value: StockMovementType; label: string }[] = [
  { value: 'In', label: 'In' },
  { value: 'Out', label: 'Out' },
]

const sourceOptions = (Object.keys(stockMovementSourceLabel) as StockMovementSource[]).map(
  (value) => ({ value, label: stockMovementSourceLabel[value] }),
)

function onProductChange(value: string | null) {
  filters.value = { ...filters.value, productId: value ?? undefined, page: 1 }
}

function onLocationChange(value: string | null) {
  filters.value = { ...filters.value, locationId: value ?? undefined, page: 1 }
}

function onTypeChange(value: StockMovementType | null) {
  filters.value = { ...filters.value, type: value ?? undefined, page: 1 }
}

function onSourceChange(value: StockMovementSource | null) {
  filters.value = { ...filters.value, source: value ?? undefined, page: 1 }
}

function setPage(page: number) {
  filters.value = { ...filters.value, page }
}

function setPageSize(pageSize: number) {
  filters.value = { ...filters.value, pageSize, page: 1 }
}
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1600px">
    <ListingHeader title="Stock Movements" :count="data?.totalCount ?? 0" :loading="isFetching" @refresh="refetch" />

    <div class="flex items-center gap-2 flex-wrap">
      <div class="w-72">
        <ProductSelect
          v-model="productFilter"
          show-clear
          placeholder="All products"
          @update:model-value="onProductChange"
        />
      </div>
      <div class="w-72">
        <LocationSelect
          v-model="locationFilter"
          show-clear
          placeholder="All locations"
          @update:model-value="onLocationChange"
        />
      </div>
      <div class="w-40">
        <Select
          v-model="typeFilter"
          :options="typeOptions"
          option-label="label"
          option-value="value"
          placeholder="All types"
          show-clear
          fluid
          @update:model-value="onTypeChange"
        />
      </div>
      <div class="w-56">
        <Select
          v-model="sourceFilter"
          :options="sourceOptions"
          option-label="label"
          option-value="value"
          placeholder="All sources"
          show-clear
          fluid
          @update:model-value="onSourceChange"
        />
      </div>
    </div>

    <DataTableWrapper
      :items="data?.items ?? []"
      :total-count="data?.totalCount ?? 0"
      :page="filters.page"
      :page-size="filters.pageSize"
      :loading="isFetching"
      @update:page="setPage"
      @update:page-size="setPageSize"
    >
      <Column header="Created" style="width: 14rem">
        <template #body="{ data: row }: { data: StockMovementDto }">
          {{ formatDateTime(row.createdAt) }}
        </template>
      </Column>
      <Column header="Product">
        <template #body="{ data: row }: { data: StockMovementDto }">
          <div class="font-medium text-surface-900">{{ row.product.sku }}</div>
          <div class="text-xs text-surface-500">{{ row.product.name }}</div>
        </template>
      </Column>
      <Column header="Location">
        <template #body="{ data: row }: { data: StockMovementDto }">
          <div>{{ row.location.code }}</div>
          <div class="text-xs text-surface-500">{{ row.location.address }}</div>
        </template>
      </Column>
      <Column header="Lot" style="width: 10rem">
        <template #body="{ data: row }: { data: StockMovementDto }">
          {{ row.lot?.number ?? '—' }}
        </template>
      </Column>
      <Column header="Qty Change" style="width: 9rem">
        <template #body="{ data: row }: { data: StockMovementDto }">
          <span
            class="font-semibold"
            :class="row.type == 'Out' ? 'text-red-600' : 'text-green-600'"
          >
            {{ row.type == 'Out' ? `-${row.quantityChange}` : `+${row.quantityChange}` }}
          </span>
        </template>
      </Column>
      <Column header="Type" style="width: 7rem">
        <template #body="{ data: row }: { data: StockMovementDto }">
          <StatusBadge :value="row.type" :severity="stockMovementTypeSeverity[row.type]" />
        </template>
      </Column>
      <Column header="Source" style="width: 13rem">
        <template #body="{ data: row }: { data: StockMovementDto }">
          <StatusBadge
            :value="stockMovementSourceLabel[row.source]"
            :severity="stockMovementSourceSeverity[row.source]"
          />
        </template>
      </Column>
    </DataTableWrapper>
  </section>
</template>
