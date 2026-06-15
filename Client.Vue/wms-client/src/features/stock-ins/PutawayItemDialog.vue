<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import { usePutawayItem } from './useStockIns'
import type { HandlingUnitRef, LocationRef, LotRef, ProductRef } from '@/types/refs'

/** The placement being put away, flattened for the dialog. */
export interface PutawayItem {
  id: string
  product: ProductRef
  lot: LotRef | null
  handlingUnit: HandlingUnitRef | null
  location: LocationRef
  quantity: number
  placedQuantity: number
}

const visible = defineModel<boolean>('visible', { default: false })
const props = defineProps<{ stockInId: string; item: PutawayItem | null }>()

const toast = useToast()
const putaway = usePutawayItem(props.stockInId)

const remaining = computed(() => (props.item ? props.item.quantity - props.item.placedQuantity : 0))

const quantity = ref<number | null>(null)
const serverError = ref<string | null>(null)

// Reset to "all remaining" each time the dialog opens.
watch(visible, (open) => {
  if (open) {
    quantity.value = remaining.value
    serverError.value = null
  }
})

const isValid = computed(
  () => quantity.value !== null && quantity.value >= 1 && quantity.value <= remaining.value,
)

function save() {
  serverError.value = null
  if (!props.item || !isValid.value || quantity.value === null) return

  putaway.mutate(
    { itemId: props.item.id, quantity: quantity.value },
    {
      onSuccess: () => {
        toast.add({ severity: 'success', summary: 'Items put away', life: 3000 })
        visible.value = false
      },
      onError: (err) => {
        serverError.value = err.message
      },
    },
  )
}
</script>

<template>
  <Dialog v-model:visible="visible" modal header="Put away" :style="{ width: '28rem' }">
    <div v-if="item" class="flex flex-col gap-4">
      <div class="text-sm text-surface-600 rounded-lg bg-surface-50 p-3">
        <div class="font-medium text-surface-900">
          {{ item.product.sku }} — {{ item.product.name }}
        </div>
        <div>{{ item.location.code }} · {{ item.location.address }}</div>
        <div v-if="item.lot">Lot {{ item.lot.number }}</div>
        <div v-if="item.handlingUnit">
          <i class="pi pi-inbox text-xs" /> Handling unit {{ item.handlingUnit.code }}
        </div>
        <div class="mt-2">
          Planned: <b>{{ item.quantity }}</b> · Placed: <b>{{ item.placedQuantity }}</b> ·
          Left: <b>{{ remaining }}</b>
        </div>
      </div>

      <Message v-if="serverError" severity="error" :closable="false">
        {{ serverError }}
      </Message>

      <div class="flex flex-col gap-1">
        <label for="putaway-qty" class="text-sm font-medium text-surface-700">
          Quantity to put away
        </label>
        <InputNumber
          input-id="putaway-qty"
          v-model="quantity"
          show-buttons
          :min="1"
          :max="remaining"
          :step="1"
          :use-grouping="false"
          fluid
          :invalid="!isValid"
        />
        <small class="text-surface-400">Up to {{ remaining }} can be placed.</small>
      </div>
    </div>

    <template #footer>
      <Button label="Cancel" severity="secondary" text @click="visible = false" />
      <Button
        label="Put away"
        icon="pi pi-check"
        :disabled="!isValid"
        :loading="putaway.isPending.value"
        @click="save"
      />
    </template>
  </Dialog>
</template>
