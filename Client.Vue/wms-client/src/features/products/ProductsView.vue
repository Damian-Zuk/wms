<script setup lang="ts">
import { ref } from 'vue'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import Tag from 'primevue/tag'
import DataTableWrapper from '@/components/common/DataTableWrapper.vue'
import { useProducts } from './useProducts'
import type { ProductDto, ProductFilters } from '@/types/products'
import type { TemperatureZone } from '@/types/enums'

const filters = ref<ProductFilters>({ search: '', page: 1, pageSize: 20 })
const search = ref('')

const { data, isFetching } = useProducts(filters)

function applySearch() {
  filters.value = { ...filters.value, search: search.value.trim(), page: 1 }
}

function setPage(page: number) {
  filters.value = { ...filters.value, page }
}

function setPageSize(pageSize: number) {
  filters.value = { ...filters.value, pageSize, page: 1 }
}

type TagSeverity = 'success' | 'info' | 'warn' | 'secondary' | 'danger' | 'contrast'

const zoneSeverity: Record<TemperatureZone, TagSeverity> = {
  Ambient: 'success',
  Chilled: 'info',
  Frozen: 'contrast',
}
</script>

<template>
  <section class="p-6 flex flex-col gap-4">
    <div class="flex items-center justify-between gap-4">
      <h1 class="text-2xl font-semibold text-surface-900">Product Catalog</h1>

      <IconField>
        <InputIcon class="pi pi-search" />
        <InputText
          v-model="search"
          placeholder="Search SKU or name"
          @keyup.enter="applySearch"
        />
      </IconField>
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
      <Column field="sku" header="SKU" style="width: 14rem" />
      <Column field="name" header="Name" />
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
