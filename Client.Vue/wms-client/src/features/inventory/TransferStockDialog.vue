<script setup lang="ts">
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import LocationSelect from '@/components/pickers/LocationSelect.vue'
import HandlingUnitSelect from '@/components/pickers/HandlingUnitSelect.vue'
import { useTransferStock } from './useInventory'
import type { InventoryDto } from '@/types/inventory'

const visible = defineModel<boolean>('visible', { default: false })
const props = defineProps<{ inventory: InventoryDto | null }>()

const toast = useToast()
const transfer = useTransferStock()

const quantity = ref<number | null>(null)
const destinationLocationId = ref<string | null>(null)
const destinationHandlingUnitId = ref<string | null>(null)
const fieldError = ref<string | null>(null)
const serverError = ref<string | null>(null)

// Reset the form each time the dialog opens.
watch(visible, (open) => {
  if (open) {
    quantity.value = null
    destinationLocationId.value = null
    destinationHandlingUnitId.value = null
    fieldError.value = null
    serverError.value = null
  }
})

// A destination unit only makes sense at the chosen destination.
watch(destinationLocationId, () => {
  destinationHandlingUnitId.value = null
})

function submit() {
  fieldError.value = null
  serverError.value = null

  if (!props.inventory) return

  if (quantity.value === null || quantity.value <= 0) {
    fieldError.value = 'Quantity must be greater than zero'
    return
  }
  if (quantity.value > props.inventory.available) {
    fieldError.value = `Quantity cannot exceed available (${props.inventory.available})`
    return
  }
  if (!destinationLocationId.value) {
    fieldError.value = 'Select a destination location'
    return
  }
  if (destinationLocationId.value === props.inventory.location.id) {
    fieldError.value = 'Destination must differ from the source location'
    return
  }

  transfer.mutate(
    {
      productId: props.inventory.product.id,
      sourceLocationId: props.inventory.location.id,
      destinationLocationId: destinationLocationId.value,
      lotId: props.inventory.lot?.id ?? null,
      quantity: quantity.value,
      sourceHandlingUnitId: props.inventory.handlingUnit?.id ?? null,
      destinationHandlingUnitId: destinationHandlingUnitId.value,
    },
    {
      onSuccess: () => {
        toast.add({ severity: 'success', summary: 'Stock transferred', life: 3000 })
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
  <Dialog v-model:visible="visible" modal header="Transfer stock" :style="{ width: '28rem' }">
    <div v-if="inventory" class="flex flex-col gap-4">
      <div class="text-sm text-surface-600 rounded-lg bg-surface-50 p-3">
        <div class="font-medium text-surface-900">
          {{ inventory.product.sku }} — {{ inventory.product.name }}
        </div>
        <div>From {{ inventory.location.code }} · {{ inventory.location.address }}</div>
        <div v-if="inventory.lot">Lot {{ inventory.lot.number }}</div>
        <div v-if="inventory.handlingUnit">
          From handling unit {{ inventory.handlingUnit.code }}
        </div>
        <div class="mt-2">
          Available to transfer: <b>{{ inventory.available }}</b>
        </div>
      </div>

      <Message v-if="serverError" severity="error" :closable="false">
        {{ serverError }}
      </Message>

      <div class="flex flex-col gap-1">
        <label for="transfer-qty" class="text-sm font-medium text-surface-700">Quantity</label>
        <InputNumber
          input-id="transfer-qty"
          v-model="quantity"
          show-buttons
          :min="1"
          :max="inventory.available"
          :step="1"
          :use-grouping="false"
          placeholder="e.g. 10"
          fluid
          :invalid="!!fieldError"
        />
      </div>

      <div class="flex flex-col gap-1">
        <label for="destination" class="text-sm font-medium text-surface-700">
          Destination location
        </label>
        <LocationSelect
          v-model="destinationLocationId"
          show-clear
          placeholder="Select destination"
        />
      </div>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium text-surface-700">
          Destination handling unit
          <span class="text-surface-400">(optional)</span>
        </label>
        <HandlingUnitSelect
          v-model="destinationHandlingUnitId"
          :location-id="destinationLocationId ?? undefined"
          placeholder="Loose stock"
        />
      </div>

      <small v-if="fieldError" class="text-red-500">{{ fieldError }}</small>
    </div>

    <template #footer>
      <Button label="Cancel" severity="secondary" text @click="visible = false" />
      <Button
        label="Transfer"
        icon="pi pi-arrow-right-arrow-left"
        :loading="transfer.isPending.value"
        @click="submit"
      />
    </template>
  </Dialog>
</template>
