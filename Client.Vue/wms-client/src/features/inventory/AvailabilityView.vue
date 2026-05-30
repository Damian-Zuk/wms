<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import ProductSelect from '@/components/pickers/ProductSelect.vue'
import LocationSelect from '@/components/pickers/LocationSelect.vue'
import InventoryTabs from './InventoryTabs.vue'
import { useAvailability } from './useInventory'
import type { AvailabilityParams } from '@/types/inventory'

const productId = ref<string | null>(null)
const locationId = ref<string | null>(null)

const params = ref<AvailabilityParams>({ productId: '' })

watch([productId, locationId], ([p, l]) => {
  params.value = { productId: p ?? '', locationId: l ?? undefined }
})

const { data, isFetching, isError, error } = useAvailability(params)

const hasProduct = computed(() => !!productId.value)
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1000px">
    <h1 class="text-2xl font-semibold text-surface-900">Inventory</h1>
    <InventoryTabs />

    <div class="flex items-end gap-2">
      <div class="w-72">
        <label class="text-sm font-medium text-surface-700">Product</label>
        <ProductSelect v-model="productId" show-clear placeholder="Select product" />
      </div>
      <div class="w-72">
        <label class="text-sm font-medium text-surface-700">Location (optional)</label>
        <LocationSelect v-model="locationId" show-clear placeholder="All locations" />
      </div>
    </div>

    <p v-if="!hasProduct" class="text-surface-500">
      Select a product to see its availability.
    </p>

    <div v-else-if="isFetching" class="flex justify-center py-10">
      <ProgressSpinner />
    </div>

    <Message v-else-if="isError" severity="error" :closable="false">
      {{ error?.message ?? 'Failed to load availability.' }}
    </Message>

    <div v-else-if="data" class="flex flex-col gap-4">
      <div class="text-surface-700">
        <span class="font-medium text-surface-900">{{ data.productSku }}</span>
        — {{ data.productName }}
      </div>
      <div class="grid grid-cols-3 gap-4">
        <div class="rounded-xl border border-surface-200 bg-white p-5 text-center">
          <div class="text-sm text-surface-500">On Hand</div>
          <div class="text-3xl font-semibold text-surface-900">{{ data.onHand }}</div>
        </div>
        <div class="rounded-xl border border-surface-200 bg-white p-5 text-center">
          <div class="text-sm text-surface-500">Reserved</div>
          <div class="text-3xl font-semibold text-surface-900">{{ data.reserved }}</div>
        </div>
        <div class="rounded-xl border border-surface-200 bg-white p-5 text-center">
          <div class="text-sm text-surface-500">Available</div>
          <div class="text-3xl font-semibold text-primary-600">{{ data.available }}</div>
        </div>
      </div>
    </div>
  </section>
</template>
