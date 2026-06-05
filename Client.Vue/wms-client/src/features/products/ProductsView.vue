<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import DataTableWrapper from '@/components/common/DataTableWrapper.vue'
import RefreshButton from '@/components/common/RefreshButton.vue'
import { useProducts } from './useProducts'
import { useAuthStore } from '@/stores/auth'
import { sortOrderOf, toSortFilters, type SortChange } from '@/lib/sort'
import type { ProductDto, ProductFilters } from '@/types/products'
import type { TemperatureZone } from '@/types/enums'

const router = useRouter()
const auth = useAuthStore()

const filters = ref<ProductFilters>({ search: '', page: 1, pageSize: 20 })
const search = ref('')

const { data, isFetching, refetch } = useProducts(filters)

function openProduct(product: ProductDto) {
  router.push({ name: 'product-detail', params: { id: product.id } })
}

function applySearch() {
  filters.value = { ...filters.value, search: search.value.trim(), page: 1 }
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

type TagSeverity = 'success' | 'info' | 'warn' | 'secondary' | 'danger' | 'contrast'

const zoneSeverity: Record<TemperatureZone, TagSeverity> = {
  Ambient: 'success',
  Chilled: 'info',
  Frozen: 'contrast',
}
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1600px">
    <div class="flex items-center justify-between gap-4">
      <div class="flex items-center gap-3">
        <h1 class="text-2xl font-semibold text-surface-900">Product Catalog</h1>
        <RefreshButton :loading="isFetching" @click="() => refetch()" />
      </div>

      <div class="flex items-center gap-2">
        <IconField>
          <InputIcon class="pi pi-search" />
          <InputText
            v-model="search"
            placeholder="Search SKU or name"
            @keyup.enter="applySearch"
          />
        </IconField>
        <Button
          v-if="auth.canMutate"
          label="New Product"
          icon="pi pi-plus"
          @click="router.push({ name: 'product-create' })"
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
      @row-click="openProduct"
    >
      <Column field="sku" header="SKU" sortable sort-field="sku" style="width: 14rem" />
      <Column field="name" header="Name" sortable sort-field="name" />
      <Column field="description" header="Description" />
      <Column header="Temperature Zone" style="width: 12rem">
        <template #body="{ data: row }: { data: ProductDto }">
          <Tag
            :value="row.requiredTemperatureZone"
            :severity="zoneSeverity[row.requiredTemperatureZone]"
          />
        </template>
      </Column>
    </DataTableWrapper>
  </section>
</template>
