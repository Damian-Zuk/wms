<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { useAvailability } from './useInventory'
import { formatCurrency } from '@/lib/money'
import type { AvailabilityParams } from '@/types/inventory'

const props = defineProps<{
  productId: string
  lotId?: string
}>()

const params = ref<AvailabilityParams>({ productId: props.productId, lotId: props.lotId })

watch(
  () => [props.productId, props.lotId] as const,
  ([productId, lotId]) => {
    params.value = { productId, lotId }
  },
)

const { data, isFetching, isError, error } = useAvailability(params)

const hasProduct = computed(() => !!props.productId)
</script>

<template>
  <div v-if="!hasProduct" class="text-surface-500">No product associated.</div>

  <div v-else-if="isFetching" class="flex justify-center py-6">
    <ProgressSpinner />
  </div>

  <Message v-else-if="isError" severity="error" :closable="false">
    {{ error?.message ?? 'Failed to load availability.' }}
  </Message>

  <div v-else-if="data" class="grid grid-cols-2 lg:grid-cols-4 gap-4">
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
    <div class="rounded-xl border border-surface-200 bg-white p-5 text-center">
      <div class="text-sm text-surface-500">On-hand Value</div>
      <div class="text-3xl font-semibold text-surface-900">{{ formatCurrency(data.onHandValue) }}</div>
    </div>
  </div>
</template>
