<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import LocationSelect from '@/components/pickers/LocationSelect.vue'
import { useModifyLinePlacements } from './useStockIns'
import type { StockInLineDto } from '@/types/stock-ins'

const visible = defineModel<boolean>('visible', { default: false })
const props = defineProps<{ stockInId: string; line: StockInLineDto | null }>()

const toast = useToast()
const modify = useModifyLinePlacements(props.stockInId)

interface Row {
  locationId: string
  quantity: number | null
}

const rows = ref<Row[]>([])
const serverError = ref<string | null>(null)

const required = computed(() => props.line?.quantity ?? 0)
const currentSum = computed(() => rows.value.reduce((sum, r) => sum + (r.quantity ?? 0), 0))
const matches = computed(() => currentSum.value === required.value)
const isValid = computed(
  () =>
    rows.value.length > 0 &&
    rows.value.every((r) => r.locationId && r.quantity && r.quantity > 0) &&
    matches.value,
)

// Seed the editor from the line's current placements each time it opens.
watch(visible, (open) => {
  if (open && props.line) {
    rows.value = props.line.placements.map((p) => ({
      locationId: p.location.id,
      quantity: p.quantity,
    }))
    serverError.value = null
  }
})

function addRow() {
  rows.value.push({ locationId: '', quantity: null })
}

function removeRow(index: number) {
  rows.value.splice(index, 1)
}

function save() {
  if (!props.line || !isValid.value) return
  serverError.value = null
  modify.mutate(
    {
      lineId: props.line.id,
      placements: rows.value.map((r) => ({
        locationId: r.locationId,
        quantity: r.quantity as number,
      })),
    },
    {
      onSuccess: () => {
        toast.add({ severity: 'success', summary: 'Placements updated', life: 3000 })
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
    header="Edit placements"
    :style="{ width: '36rem' }"
  >
    <div v-if="line" class="flex flex-col gap-4">
      <div class="text-sm text-surface-600">
        <span class="font-medium text-surface-900">{{ line.product.sku }}</span>
        — {{ line.product.name }}
        <span v-if="line.lot"> · Lot {{ line.lot.number }}</span>
      </div>

      <Message v-if="serverError" severity="error" :closable="false">{{ serverError }}</Message>

      <div class="flex flex-col gap-2">
        <div
          v-for="(row, i) in rows"
          :key="i"
          class="grid grid-cols-[1fr_8rem_auto] gap-2 items-center"
        >
          <LocationSelect
            :model-value="row.locationId"
            placeholder="Location"
            @update:model-value="(v) => (row.locationId = v ?? '')"
          />
          <InputNumber v-model="row.quantity" :min="1" :use-grouping="false" fluid />
          <Button
            icon="pi pi-trash"
            severity="danger"
            text
            :disabled="rows.length === 1"
            aria-label="Remove placement"
            @click="removeRow(i)"
          />
        </div>
      </div>

      <div class="flex items-center justify-between">
        <Button label="Add placement" icon="pi pi-plus" severity="secondary" text @click="addRow" />
        <span class="text-sm" :class="matches ? 'text-green-600' : 'text-red-600'">
          Placed {{ currentSum }} / {{ required }}
        </span>
      </div>
    </div>

    <template #footer>
      <Button label="Cancel" severity="secondary" text @click="visible = false" />
      <Button
        label="Save"
        icon="pi pi-check"
        :disabled="!isValid"
        :loading="modify.isPending.value"
        @click="save"
      />
    </template>
  </Dialog>
</template>
