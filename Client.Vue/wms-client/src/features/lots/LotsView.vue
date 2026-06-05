<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import Button from 'primevue/button'
import DataTableWrapper from '@/components/common/DataTableWrapper.vue'
import RefreshButton from '@/components/common/RefreshButton.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import ProductSelect from '@/components/pickers/ProductSelect.vue'
import { useLots } from './useLots'
import { lotStatus } from './lot-status'
import { useProductOptions } from '@/features/products/useProducts'
import { useAuthStore } from '@/stores/auth'
import { formatDate } from '@/lib/date'
import { sortOrderOf, toSortFilters, type SortChange } from '@/lib/sort'
import type { LotDto, LotFilters } from '@/types/lots'

const router = useRouter()
const auth = useAuthStore()

const filters = ref<LotFilters>({ search: '', page: 1, pageSize: 20 })
const search = ref('')
const productFilter = ref<string | null>(null)

const { data, isFetching, refetch } = useLots(filters)
const { byId } = useProductOptions()

function productSku(id: string) {
  return byId.value.get(id)?.sku ?? '—'
}

function applySearch() {
  filters.value = { ...filters.value, search: search.value.trim(), page: 1 }
}

function onProductChange(value: string | null) {
  filters.value = { ...filters.value, productId: value ?? undefined, page: 1 }
}

function setPage(page: number) {
  filters.value = { ...filters.value, page }
}

function setPageSize(pageSize: number) {
  filters.value = { ...filters.value, pageSize, page: 1 }
}

function onSort(change: SortChange) {
  filters.value = { ...filters.value, ...toSortFilters(change), page: 1 }
}

function openLot(lot: LotDto) {
  router.push({ name: 'lot-detail', params: { id: lot.id } })
}
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1600px">
    <div class="flex items-center justify-between gap-4">
      <div class="flex items-center gap-3">
        <h1 class="text-2xl font-semibold text-surface-900">Lots</h1>
        <RefreshButton :loading="isFetching" @click="() => refetch()" />
      </div>

      <div class="flex items-center gap-2">
        <div class="w-64">
          <ProductSelect
            v-model="productFilter"
            show-clear
            placeholder="All products"
            @update:model-value="onProductChange"
          />
        </div>
        <IconField>
          <InputIcon class="pi pi-search" />
          <InputText
            v-model="search"
            placeholder="Search lot number"
            @keyup.enter="applySearch"
          />
        </IconField>
        <Button
          v-if="auth.canMutate"
          label="New Lot"
          icon="pi pi-plus"
          @click="router.push({ name: 'lot-create' })"
        />
      </div>
    </div>

    <DataTableWrapper
      :items="data?.items ?? []"
      :total-count="data?.totalCount ?? 0"
      :page="filters.page"
      :page-size="filters.pageSize"
      :loading="isFetching"
      :sort-field="filters.sortBy ?? null"
      :sort-order="sortOrderOf(filters)"
      class="cursor-pointer"
      @update:page="setPage"
      @update:page-size="setPageSize"
      @update:sort="onSort"
      @row-click="openLot"
    >
      <Column field="number" header="Lot Number" sortable sort-field="number" style="width: 16rem" />
      <Column header="Product" sortable sort-field="product">
        <template #body="{ data: row }: { data: LotDto }">
          {{ productSku(row.productId) }}
        </template>
      </Column>
      <Column header="Manufactured" sortable sort-field="manufactureDate" style="width: 12rem">
        <template #body="{ data: row }: { data: LotDto }">
          {{ formatDate(row.manufactureDate) }}
        </template>
      </Column>
      <Column header="Expires" sortable sort-field="expirationDate" style="width: 12rem">
        <template #body="{ data: row }: { data: LotDto }">
          {{ formatDate(row.expirationDate) }}
        </template>
      </Column>
      <Column header="Status" style="width: 11rem">
        <template #body="{ data: row }: { data: LotDto }">
          <StatusBadge :value="lotStatus(row).label" :severity="lotStatus(row).severity" />
        </template>
      </Column>
    </DataTableWrapper>
  </section>
</template>
