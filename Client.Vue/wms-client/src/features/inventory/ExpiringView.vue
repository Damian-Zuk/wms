<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import InputNumber from 'primevue/inputnumber'
import Message from 'primevue/message'
import InventoryTabs from './InventoryTabs.vue'
import { useExpiringInventory } from './useInventory'
import type { ExpiringInventoryLineDto } from '@/types/inventory'

const router = useRouter()
const withinDays = ref<number>(30)

const { data, isFetching, isError, error } = useExpiringInventory(withinDays)

function openInventory(row: ExpiringInventoryLineDto) {
  router.push({ name: 'inventory-detail', params: { id: row.inventoryId } })
}
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1400px">
    <h1 class="text-2xl font-semibold text-surface-900">Inventory</h1>
    <InventoryTabs />

    <div class="flex items-center gap-2">
      <label for="withinDays" class="text-sm font-medium text-surface-700">
        Expiring within (days)
      </label>
      <InputNumber
        input-id="withinDays"
        v-model="withinDays"
        :min="0"
        :allow-empty="false"
        show-buttons
      />
    </div>

    <Message v-if="isError" severity="error" :closable="false">
      {{ error?.message ?? 'Failed to load expiring inventory.' }}
    </Message>

    <DataTable
      v-else
      :value="data ?? []"
      :loading="isFetching"
      data-key="inventoryId"
      paginator
      :rows="20"
      :rows-per-page-options="[10, 20, 50]"
      :row-hover="true"
      class="cursor-pointer"
      @row-click="openInventory($event.data)"
    >
      <Column header="Expires" style="width: 12rem">
        <template #body="{ data: row }: { data: ExpiringInventoryLineDto }">
          {{ row.expirationDate }}
        </template>
      </Column>
      <Column field="productSku" header="Product" />
      <Column field="lotNumber" header="Lot" style="width: 14rem" />
      <Column field="locationCode" header="Location" style="width: 12rem" />
      <Column field="available" header="Available" style="width: 9rem" />
      <template #empty>
        <div class="p-4 text-center text-surface-500">No inventory expiring in this window.</div>
      </template>
    </DataTable>
  </section>
</template>
