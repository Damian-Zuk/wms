<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
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
  /** Set = a declared handling unit: the location may change, the quantity may not. */
  handlingUnitId: string | null
  handlingUnitCode: string | null
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

const looseRowCount = computed(() => rows.value.filter((r) => !r.handlingUnitId).length)

// Seed the editor from the line's current placements each time it opens.
watch(visible, (open) => {
  if (open && props.line) {
    rows.value = props.line.placements.map((p) => ({
      locationId: p.location.id,
      quantity: p.quantity,
      handlingUnitId: p.handlingUnit?.id ?? null,
      handlingUnitCode: p.handlingUnit?.code ?? null,
    }))
    serverError.value = null
  }
})

function addRow() {
  rows.value.push({ locationId: '', quantity: null, handlingUnitId: null, handlingUnitCode: null })
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
        handlingUnitId: r.handlingUnitId,
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
    :style="{ width: '38rem' }"
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
          <div class="flex items-center gap-2">
            <Tag
              v-if="row.handlingUnitCode"
              :value="row.handlingUnitCode"
              severity="info"
              icon="pi pi-inbox"
              class="shrink-0"
            />
            <LocationSelect
              :model-value="row.locationId"
              placeholder="Location"
              class="flex-1"
              @update:model-value="(v) => (row.locationId = v ?? '')"
            />
          </div>
          <InputNumber
            v-model="row.quantity"
            :min="1"
            :use-grouping="false"
            :disabled="!!row.handlingUnitId"
            fluid
          />
          <Button
            icon="pi pi-trash"
            severity="danger"
            text
            :disabled="!!row.handlingUnitId || looseRowCount <= 0 || rows.length === 1"
            aria-label="Remove placement"
            @click="removeRow(i)"
          />
        </div>
      </div>

      <Message
        v-if="rows.some((r) => r.handlingUnitId)"
        severity="secondary"
        :closable="false"
      >
        Handling units keep their declared quantity — only their location can change.
      </Message>

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
