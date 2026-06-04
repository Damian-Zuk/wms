<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import Column from 'primevue/column'
import Button from 'primevue/button'
import DataTableWrapper from '@/components/common/DataTableWrapper.vue'
import ProductSelect from '@/components/pickers/ProductSelect.vue'
import LocationSelect from '@/components/pickers/LocationSelect.vue'
import InventoryTabs from './InventoryTabs.vue'
import AdjustInventoryDialog from './AdjustInventoryDialog.vue'
import TransferStockDialog from './TransferStockDialog.vue'
import { useInventories } from './useInventory'
import { useAuthStore } from '@/stores/auth'
import type { InventoryDto, InventoryFilters } from '@/types/inventory'

const router = useRouter()
const auth = useAuthStore()

const filters = ref<InventoryFilters>({ page: 1, pageSize: 20 })
const productFilter = ref<string | null>(null)
const locationFilter = ref<string | null>(null)

const { data, isFetching } = useInventories(filters)

const adjustVisible = ref(false)
const adjustTarget = ref<InventoryDto | null>(null)

const transferVisible = ref(false)
const transferTarget = ref<InventoryDto | null>(null)

function onProductChange(value: string | null) {
  filters.value = { ...filters.value, productId: value ?? undefined, page: 1 }
}

function onLocationChange(value: string | null) {
  filters.value = { ...filters.value, locationId: value ?? undefined, page: 1 }
}

function setPage(page: number) {
  filters.value = { ...filters.value, page }
}

function setPageSize(pageSize: number) {
  filters.value = { ...filters.value, pageSize, page: 1 }
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
  <section class="p-6 flex flex-col gap-4" style="max-width: 1400px">
    <h1 class="text-2xl font-semibold text-surface-900">Inventory</h1>
    <InventoryTabs />

    <div class="flex items-center gap-2">
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
      @row-click="openDetail"
    >
      <Column header="Product">
        <template #body="{ data: row }: { data: InventoryDto }">
          <div class="font-medium text-surface-900">{{ row.product.sku }}</div>
          <div class="text-xs text-surface-500">{{ row.product.name }}</div>
        </template>
      </Column>
      <Column header="Location">
        <template #body="{ data: row }: { data: InventoryDto }">
          <div>{{ row.location.code }}</div>
          <div class="text-xs text-surface-500">{{ row.location.address }}</div>
        </template>
      </Column>
      <Column header="Lot" style="width: 12rem">
        <template #body="{ data: row }: { data: InventoryDto }">
          {{ row.lot?.number ?? '—' }}
        </template>
      </Column>
      <Column field="onHand" header="On Hand" style="width: 8rem" />
      <Column field="reserved" header="Reserved" style="width: 8rem" />
      <Column header="Available" style="width: 8rem">
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
