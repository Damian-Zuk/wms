<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Column from 'primevue/column'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import DataTableWrapper from '@/components/common/DataTableWrapper.vue'
import RefreshButton from '@/components/common/RefreshButton.vue'
import ProductSelect from '@/components/pickers/ProductSelect.vue'
import LocationSelect from '@/components/pickers/LocationSelect.vue'
import LotSelect from '@/components/pickers/LotSelect.vue'
import CategorySelect from '@/components/pickers/CategorySelect.vue'
import AdjustInventoryDialog from './AdjustInventoryDialog.vue'
import TransferStockDialog from './TransferStockDialog.vue'
import { useInventories } from './useInventory'
import { useProductOptions } from '@/features/products/useProducts'
import { useAuthStore } from '@/stores/auth'
import { formatDate } from '@/lib/date'
import { sortOrderOf, toSortFilters, type SortChange } from '@/lib/sort'
import type { InventoryDto, InventoryFilters } from '@/types/inventory'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

// Seed the filters from the query string so "Check inventory" links from the
// product/lot/location detail pages land pre-filtered.
const productFilter = ref<string | null>((route.query.productId as string) ?? null)
const locationFilter = ref<string | null>((route.query.locationId as string) ?? null)
const lotFilter = ref<string | null>((route.query.lotId as string) ?? null)
const categoryFilter = ref<string | null>((route.query.categoryId as string) ?? null)
const expiringWithinDays = ref<number | null>(
  route.query.expiringWithinDays != null ? Number(route.query.expiringWithinDays) : null,
)

const filters = ref<InventoryFilters>({
  productId: productFilter.value ?? undefined,
  locationId: locationFilter.value ?? undefined,
  lotId: lotFilter.value ?? undefined,
  categoryId: categoryFilter.value ?? undefined,
  expiringWithinDays: expiringWithinDays.value ?? undefined,
  page: 1,
  pageSize: 20,
})

const { data, isFetching, refetch } = useInventories(filters)
const { byId: productsById } = useProductOptions()

function productCategory(id: string) {
  return productsById.value.get(id)?.categoryName ?? '—'
}

const adjustVisible = ref(false)
const adjustTarget = ref<InventoryDto | null>(null)

const transferVisible = ref(false)
const transferTarget = ref<InventoryDto | null>(null)

function onProductChange(value: string | null) {
  // Lot is product-scoped; clear it when the product changes.
  lotFilter.value = null
  filters.value = {
    ...filters.value,
    productId: value ?? undefined,
    lotId: undefined,
    page: 1,
  }
}

function onLocationChange(value: string | null) {
  filters.value = { ...filters.value, locationId: value ?? undefined, page: 1 }
}

function onLotChange(value: string | null) {
  filters.value = { ...filters.value, lotId: value ?? undefined, page: 1 }
}

function onCategoryChange(value: string | null) {
  filters.value = { ...filters.value, categoryId: value ?? undefined, page: 1 }
}

function onExpiringChange(value: number | null) {
  filters.value = { ...filters.value, expiringWithinDays: value ?? undefined, page: 1 }
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

function openDetail(row: InventoryDto) {
  router.push({ name: 'inventory-detail', params: { id: row.id } })
}

function openAdjust(row: InventoryDto) {
  adjustTarget.value = row
  adjustVisible.value = true
}

function openTransfer(row: InventoryDto) {
  transferTarget.value = row
  transferVisible.value = true
}
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1600px">
    <div class="flex items-center gap-3">
      <h1 class="text-2xl font-semibold text-surface-900">Inventory</h1>
      <RefreshButton :loading="isFetching" @click="() => refetch()" />
    </div>

    <div class="flex flex-wrap items-end gap-2">
      <div class="w-72">
        <label class="text-sm font-medium text-surface-700">Location</label>
        <LocationSelect
          v-model="locationFilter"
          show-clear
          placeholder="All locations"
          @update:model-value="onLocationChange"
        />
      </div>
      <div class="w-72">
        <label class="text-sm font-medium text-surface-700">Product</label>
        <ProductSelect
          v-model="productFilter"
          show-clear
          placeholder="All products"
          @update:model-value="onProductChange"
        />
      </div>
      <div class="w-56">
        <label class="text-sm font-medium text-surface-700">Category</label>
        <CategorySelect
          v-model="categoryFilter"
          show-clear
          placeholder="All categories"
          @update:model-value="onCategoryChange"
        />
      </div>
      <div class="w-56">
        <label class="text-sm font-medium text-surface-700">Lot</label>
        <LotSelect
          v-model="lotFilter"
          :product-id="productFilter ?? undefined"
          show-clear
          placeholder="All lots"
          @update:model-value="onLotChange"
        />
      </div>
      <div class="w-56">
        <label for="expiringWithinDays" class="text-sm font-medium text-surface-700">
          Expiring within (days)
        </label>
        <InputNumber
          input-id="expiringWithinDays"
          v-model="expiringWithinDays"
          :min="0"
          :allow-empty="true"
          show-buttons
          placeholder="Any"
          fluid
          @update:model-value="onExpiringChange"
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
      @row-click="openDetail"
    >
      <Column header="Product" sortable sort-field="product">
        <template #body="{ data: row }: { data: InventoryDto }">
          <div class="font-medium text-surface-900">{{ row.product.sku }}</div>
          <div class="text-xs text-surface-500">{{ row.product.name }}</div>
        </template>
      </Column>
      <Column header="Category" style="width: 12rem">
        <template #body="{ data: row }: { data: InventoryDto }">
          {{ productCategory(row.product.id) }}
        </template>
      </Column>
      <Column header="Location" sortable sort-field="location">
        <template #body="{ data: row }: { data: InventoryDto }">
          <div>{{ row.location.code }}</div>
          <div class="text-xs text-surface-500">{{ row.location.address }}</div>
        </template>
      </Column>
      <Column header="Lot" sortable sort-field="lot" style="width: 12rem">
        <template #body="{ data: row }: { data: InventoryDto }">
          {{ row.lot?.number ?? '—' }}
        </template>
      </Column>
      <Column header="Expires" sortable sort-field="expirationDate" style="width: 9rem">
        <template #body="{ data: row }: { data: InventoryDto }">
          {{ formatDate(row.expirationDate) }}
        </template>
      </Column>
      <Column field="onHand" header="On Hand" sortable sort-field="onHand" style="width: 8rem" />
      <Column field="reserved" header="Reserved" sortable sort-field="reserved" style="width: 8rem" />
      <Column header="Available" sortable sort-field="available" style="width: 8rem">
        <template #body="{ data: row }: { data: InventoryDto }">
          <span class="font-semibold">{{ row.available }}</span>
        </template>
      </Column>
      <Column v-if="auth.canMutate" header="" style="width: 13rem">
        <template #body="{ data: row }: { data: InventoryDto }">
          <div class="flex gap-2">
            <Button
              label="Adjust"
              icon="pi pi-sliders-h"
              size="small"
              severity="secondary"
              outlined
              @click.stop="openAdjust(row)"
            />
            <Button
              label="Transfer"
              icon="pi pi-arrow-right-arrow-left"
              size="small"
              severity="secondary"
              outlined
              @click.stop="openTransfer(row)"
            />
          </div>
        </template>
      </Column>
    </DataTableWrapper>

    <AdjustInventoryDialog v-model:visible="adjustVisible" :inventory="adjustTarget" />
    <TransferStockDialog v-model:visible="transferVisible" :inventory="transferTarget" />
  </section>
</template>
