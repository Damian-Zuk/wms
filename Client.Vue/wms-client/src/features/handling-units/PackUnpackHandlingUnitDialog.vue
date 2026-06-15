<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import ProductSelect from '@/components/pickers/ProductSelect.vue'
import LotSelect from '@/components/pickers/LotSelect.vue'
import { usePackHandlingUnit, useUnpackHandlingUnit } from './useHandlingUnits'
import type { HandlingUnitDto } from '@/types/handling-units'

const visible = defineModel<boolean>('visible', { default: false })
const props = defineProps<{
  unit: HandlingUnitDto | null
  mode: 'pack' | 'unpack'
}>()

const toast = useToast()
const unitId = computed(() => props.unit?.id ?? '')
const pack = usePackHandlingUnit(unitId)
const unpack = useUnpackHandlingUnit(unitId)

const productId = ref<string | null>(null)
const lotId = ref<string | null>(null)
const quantity = ref<number | null>(null)
const fieldError = ref<string | null>(null)
const serverError = ref<string | null>(null)

const isPack = computed(() => props.mode === 'pack')
const mutation = computed(() => (isPack.value ? pack : unpack))

/** Unpacking can only draw from what the unit holds. */
const unpackSource = computed(() => {
  if (isPack.value || !props.unit) return null
  return (
    props.unit.contents.find(
      (c) => c.product.id === productId.value && (c.lot?.id ?? null) === lotId.value,
    ) ?? null
  )
})

watch(visible, (open) => {
  if (open) {
    productId.value = null
    lotId.value = null
    quantity.value = null
    fieldError.value = null
    serverError.value = null
  }
})

// Lot follows product.
watch(productId, () => {
  lotId.value = null
})

function submit() {
  fieldError.value = null
  serverError.value = null

  if (!props.unit) return

  if (!productId.value) {
    fieldError.value = 'Select a product'
    return
  }
  if (quantity.value === null || quantity.value <= 0) {
    fieldError.value = 'Quantity must be greater than zero'
    return
  }
  if (!isPack.value && unpackSource.value && quantity.value > unpackSource.value.available) {
    fieldError.value = `Quantity cannot exceed available (${unpackSource.value.available})`
    return
  }

  mutation.value.mutate(
    { productId: productId.value, lotId: lotId.value, quantity: quantity.value },
    {
      onSuccess: () => {
        toast.add({
          severity: 'success',
          summary: isPack.value ? 'Stock packed onto unit' : 'Stock unpacked off unit',
          life: 3000,
        })
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
  <Dialog
    v-model:visible="visible"
    modal
    :header="isPack ? 'Pack stock onto unit' : 'Unpack stock off unit'"
    :style="{ width: '28rem' }"
  >
    <div v-if="unit" class="flex flex-col gap-4">
      <div class="text-sm text-surface-600 rounded-lg bg-surface-50 p-3">
        <div class="font-medium text-surface-900">{{ unit.code }}</div>
        <div v-if="unit.location">
          At {{ unit.location.code }} · {{ unit.location.address }}
        </div>
        <div class="mt-2">
          {{
            isPack
              ? 'Moves loose stock standing at this location onto the unit.'
              : 'Moves stock off the unit back to loose stock at this location.'
          }}
        </div>
      </div>

      <Message v-if="serverError" severity="error" :closable="false">
        {{ serverError }}
      </Message>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium text-surface-700">Product</label>
        <ProductSelect v-model="productId" placeholder="Select product" />
      </div>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium text-surface-700">Lot</label>
        <LotSelect v-model="lotId" :product-id="productId ?? undefined" />
      </div>

      <div class="flex flex-col gap-1">
        <label for="pk-qty" class="text-sm font-medium text-surface-700">Quantity</label>
        <InputNumber
          input-id="pk-qty"
          v-model="quantity"
          show-buttons
          :min="1"
          :max="unpackSource?.available ?? undefined"
          :step="1"
          :use-grouping="false"
          placeholder="e.g. 10"
          fluid
          :invalid="!!fieldError"
        />
        <small v-if="!isPack && unpackSource" class="text-surface-500">
          On unit: {{ unpackSource.available }} available
        </small>
      </div>

      <small v-if="fieldError" class="text-red-500">{{ fieldError }}</small>
    </div>

    <template #footer>
      <Button label="Cancel" severity="secondary" text @click="visible = false" />
      <Button
        :label="isPack ? 'Pack' : 'Unpack'"
        :icon="isPack ? 'pi pi-download' : 'pi pi-upload'"
        :loading="mutation.isPending.value"
        @click="submit"
      />
    </template>
  </Dialog>
</template>
