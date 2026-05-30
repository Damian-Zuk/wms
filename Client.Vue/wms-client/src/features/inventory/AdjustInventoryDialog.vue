<script setup lang="ts">
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import { useAdjustInventory } from './useInventory'
import type { InventoryDto } from '@/types/inventory'

const visible = defineModel<boolean>('visible', { default: false })
const props = defineProps<{ inventory: InventoryDto | null }>()

const toast = useToast()
const adjust = useAdjustInventory()

const quantityChange = ref<number | null>(null)
const reason = ref('')
const fieldError = ref<string | null>(null)
const serverError = ref<string | null>(null)

// Reset the form each time the dialog opens.
watch(visible, (open) => {
  if (open) {
    quantityChange.value = null
    reason.value = ''
    fieldError.value = null
    serverError.value = null
  }
})

function submit() {
  fieldError.value = null
  serverError.value = null

  if (quantityChange.value === null || quantityChange.value === 0) {
    fieldError.value = 'Quantity change must not be zero'
    return
  }
  if (!props.inventory) return

  adjust.mutate(
    {
      id: props.inventory.id,
      body: { quantityChange: quantityChange.value, reason: reason.value.trim() || null },
    },
    {
      onSuccess: () => {
        toast.add({ severity: 'success', summary: 'Inventory adjusted', life: 3000 })
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
  <Dialog v-model:visible="visible" modal header="Adjust inventory" :style="{ width: '28rem' }">
    <div v-if="inventory" class="flex flex-col gap-4">
      <div class="text-sm text-surface-600 rounded-lg bg-surface-50 p-3">
        <div class="font-medium text-surface-900">
          {{ inventory.product.sku }} — {{ inventory.product.name }}
        </div>
        <div>{{ inventory.location.code }} · {{ inventory.location.address }}</div>
        <div v-if="inventory.lot">Lot {{ inventory.lot.number }}</div>
        <div class="mt-2">
          On hand: <b>{{ inventory.onHand }}</b> · Reserved: <b>{{ inventory.reserved }}</b> ·
          Available: <b>{{ inventory.available }}</b>
        </div>
      </div>

      <Message v-if="serverError" severity="error" :closable="false">
        {{ serverError }}
      </Message>

      <div class="flex flex-col gap-1">
        <label for="qty" class="text-sm font-medium text-surface-700">Quantity change</label>
        <InputNumber
          input-id="qty"
          v-model="quantityChange"
          show-buttons
          :step="1"
          :use-grouping="false"
          placeholder="e.g. -5 or 10"
          fluid
          :invalid="!!fieldError"
        />
        <small v-if="fieldError" class="text-red-500">{{ fieldError }}</small>
        <small v-else class="text-surface-400">Positive adds stock, negative removes.</small>
      </div>

      <div class="flex flex-col gap-1">
        <label for="reason" class="text-sm font-medium text-surface-700">Reason</label>
        <Textarea id="reason" v-model="reason" rows="2" :maxlength="500" fluid />
      </div>
    </div>

    <template #footer>
      <Button label="Cancel" severity="secondary" text @click="visible = false" />
      <Button
        label="Apply"
        icon="pi pi-check"
        :loading="adjust.isPending.value"
        @click="submit"
      />
    </template>
  </Dialog>
</template>
