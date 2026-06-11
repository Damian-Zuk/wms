<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Select from 'primevue/select'
import DataTableWrapper from '@/components/common/DataTableWrapper.vue'
import RefreshButton from '@/components/common/RefreshButton.vue'
import CategorySelect from '@/components/pickers/CategorySelect.vue'
import { useProducts } from './useProducts'
import { useAuthStore } from '@/stores/auth'
import { formatCurrency } from '@/lib/money'
import { sortOrderOf, toSortFilters, type SortChange } from '@/lib/sort'
import type { ProductDto, ProductFilters } from '@/types/products'
import type { TemperatureZone } from '@/types/enums'
import { temperatureZoneSeverity } from '@/lib/enum-display'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const temperatureZoneOptions: { label: string; value: TemperatureZone }[] = [
  { label: 'Ambient', value: 'Ambient' },
  { label: 'Chilled', value: 'Chilled' },
  { label: 'Frozen', value: 'Frozen' },
]

// Seed the category filter from the query string so "View in catalog" links
// from the category tree / product detail land pre-filtered.
const categoryFilter = ref<string | null>((route.query.categoryId as string) ?? null)
const filters = ref<ProductFilters>({
  search: '',
  categoryId: categoryFilter.value ?? undefined,
  page: 1,
  pageSize: 20,
})
const search = ref('')

const { data, isFetching, refetch } = useProducts(filters)

// Keep the filter in sync when the category query param changes while the view
// is already mounted (e.g. navigating here from another "View in catalog" link).
watch(
  () => route.query.categoryId,
  (value) => {
    const id = (value as string) ?? null
    if (id !== (filters.value.categoryId ?? null)) {
      categoryFilter.value = id
      filters.value = { ...filters.value, categoryId: id ?? undefined, page: 1 }
    }
  },
)

function openProduct(product: ProductDto) {
  router.push({ name: 'product-detail', params: { id: product.id } })
}

function applySearch() {
  filters.value = { ...filters.value, search: search.value.trim(), page: 1 }
}

function onCategoryChange(value: string | null) {
  filters.value = { ...filters.value, categoryId: value ?? undefined, page: 1 }
}

function onTempZoneChange(value: TemperatureZone | null) {
  filters.value = { ...filters.value, temperatureZone: value ?? undefined, page: 1 }
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

</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1600px">
    <div class="flex items-center justify-between gap-4">
      <div class="flex items-center gap-3">
        <h1 class="text-2xl font-semibold text-surface-900">Product Catalog</h1>
        <RefreshButton :loading="isFetching" @click="() => refetch()" />
      </div>

      <div class="flex items-center gap-2">
        <div class="w-56">
          <CategorySelect
            v-model="categoryFilter"
            show-clear
            placeholder="All categories"
            @update:model-value="onCategoryChange"
          />
        </div>
        <Select
          :options="temperatureZoneOptions"
          option-label="label"
          option-value="value"
          placeholder="All temp. zones"
          show-clear
          @update:model-value="onTempZoneChange"
        />
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
      <Column header="Category" sortable sort-field="category" style="width: 13rem">
        <template #body="{ data: row }: { data: ProductDto }">
          <span v-if="row.categoryName" class="text-surface-700">{{ row.categoryName }}</span>
          <span v-else class="text-surface-400">—</span>
        </template>
      </Column>
      <Column header="Weight" sortable sort-field="weight" style="width: 9rem">
        <template #body="{ data: row }: { data: ProductDto }">
          <span class="text-surface-700">{{ row.weight }} kg</span>
        </template>
      </Column>
      <Column header="Volume" sortable sort-field="volume" style="width: 9rem">
        <template #body="{ data: row }: { data: ProductDto }">
          <span class="text-surface-700">{{ row.volume }} dm³</span>
        </template>
      </Column>
      <Column header="Temp. Zone" sortable sort-field="temperatureZone" style="width: 11rem">
        <template #body="{ data: row }: { data: ProductDto }">
          <Tag
            :value="row.requiredTemperatureZone"
            :severity="temperatureZoneSeverity[row.requiredTemperatureZone]"
          />
        </template>
      </Column>
      <Column header="On Hand" sortable sort-field="onhand" style="width: 9rem">
        <template #body="{ data: row }: { data: ProductDto }">
          <span class="text-surface-700">{{ row.onHand }}</span>
        </template>
      </Column>
      <Column header="Unit Price" sortable sort-field="unitPrice" style="width: 10rem">
        <template #body="{ data: row }: { data: ProductDto }">
          <span class="text-surface-900 font-medium">{{ formatCurrency(row.unitPrice) }}</span>
        </template>
      </Column>
    </DataTableWrapper>
  </section>
</template>
