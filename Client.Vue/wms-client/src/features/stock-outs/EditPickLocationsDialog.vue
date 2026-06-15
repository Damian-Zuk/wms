<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import Dialog from 'primevue/dialog'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import Tag from 'primevue/tag'
import { useToast } from 'primevue/usetoast'
import { inventoryApi } from '@/api/endpoints/inventory'
import { qk } from '@/api/query-keys'
import { useModifyPickLocations } from './useStockOuts'
import type { StockOutLineDto } from '@/types/stock-outs'
import type { InventoryFilters } from '@/types/inventory'
import type { HandlingUnitRef, LocationRef, LotRef } from '@/types/refs'

const visible = defineModel<boolean>('visible', { default: false })
const props = defineProps<{ stockOutId: string; line: StockOutLineDto | null }>()

const toast = useToast()
const modify = useModifyPickLocations(props.stockOutId)

/** One inventory source the line can draw from. */
interface PickableRow {
  key: string
  locationId: string
  location: LocationRef
  lotId: string | null
  lot: LotRef | null
  handlingUnitId: string | null
  handlingUnit: HandlingUnitRef | null
  available: number
}

interface SelectedEntry {
  locationId: string
  lotId: string | null
  handlingUnitId: string | null
  quantity: number
}

// A source is identified by its location + lot + handling unit (each may be absent).
function keyOf(locationId: string, lotId: string | null, handlingUnitId: string | null) {
  return `${locationId}|${lotId ?? ''}|${handlingUnitId ?? ''}`
}

const serverError = ref<string | null>(null)
// Chosen quantities, keyed by source. Seeded from the line's current picks on open.
const selected = ref<Record<string, SelectedEntry>>({})

// Pull every inventory source for the line's product while the dialog is open.
const inventoryFilters = computed<InventoryFilters>(() => ({
  productId: props.line?.product.id ?? '',
  page: 1,
  pageSize: 100,
}))
const {
  data: inventoryPage,
  isLoading: inventoryLoading,
  isError: inventoryError,
} = useQuery({
  queryKey: computed(() => qk.inventories.list(inventoryFilters.value)),
  queryFn: () => inventoryApi.list(inventoryFilters.value),
  enabled: computed(() => visible.value && !!props.line),
  staleTime: 0,
})

// The line's own reservation at a source (added back to Available so the user can
// keep or move what this line already holds — CreateStockOut reserved it).
function reservedByLine(locationId: string, lotId: string | null, handlingUnitId: string | null) {
  if (!props.line) return 0
  return props.line.items
    .filter(
      (it) =>
        it.location.id === locationId &&
        (it.lot?.id ?? null) === lotId &&
        (it.handlingUnit?.id ?? null) === handlingUnitId,
    )
    .reduce((sum, it) => sum + it.quantity, 0)
}

const rows = computed<PickableRow[]>(() => {
  const items = inventoryPage.value?.items ?? []
  return items
    .map((inv) => {
      const lotId = inv.lot?.id ?? null
      const handlingUnitId = inv.handlingUnit?.id ?? null
      return {
        key: keyOf(inv.location.id, lotId, handlingUnitId),
        locationId: inv.location.id,
        location: inv.location,
        lotId,
        lot: inv.lot,
        handlingUnitId,
        handlingUnit: inv.handlingUnit,
        available: inv.available + reservedByLine(inv.location.id, lotId, handlingUnitId),
      }
    })
    .sort(
      (a, b) =>
        b.available - a.available ||
        (a.location.address < b.location.address ? -1 : a.location.address > b.location.address ? 1 : 0),
    )
})

const required = computed(() => props.line?.quantity ?? 0)
const selectedTotal = computed(() =>
  Object.values(selected.value).reduce((sum, e) => sum + e.quantity, 0),
)
const matches = computed(() => selectedTotal.value === required.value)
const canSave = computed(() => selectedTotal.value > 0 && matches.value)

function selectedQty(row: PickableRow) {
  return selected.value[row.key]?.quantity ?? 0
}

// Seed the editor from the line's current picks each time it opens.
watch(visible, (open) => {
  if (open && props.line) {
    const seed: Record<string, SelectedEntry> = {}
    for (const it of props.line.items) {
      const lotId = it.lot?.id ?? null
      const handlingUnitId = it.handlingUnit?.id ?? null
      const key = keyOf(it.location.id, lotId, handlingUnitId)
      seed[key] = {
        locationId: it.location.id,
        lotId,
        handlingUnitId,
        quantity: (seed[key]?.quantity ?? 0) + it.quantity,
      }
    }
    selected.value = seed
    serverError.value = null
    amountVisible.value = false
  }
})

// --- Amount sub-dialog: pick how many units to take from one source. ---
const amountVisible = ref(false)
const amountTarget = ref<PickableRow | null>(null)
const amountQty = ref<number | null>(null)

const isAmountValid = computed(() => {
  const row = amountTarget.value
  const qty = amountQty.value
  return !!row && qty !== null && qty >= 1 && qty <= row.available
})

function openAmount(row: PickableRow) {
  amountTarget.value = row
  const current = selectedQty(row)
  if (current > 0) {
    amountQty.value = current
  } else {
    // Pre-fill with what's still needed, capped at this source's availability.
    const remaining = required.value - selectedTotal.value
    amountQty.value = Math.min(row.available, Math.max(1, remaining))
  }
  amountVisible.value = true
}

function confirmAmount() {
  const row = amountTarget.value
  const qty = amountQty.value
  if (!row || qty === null || !isAmountValid.value) return
  selected.value = {
    ...selected.value,
    [row.key]: {
      locationId: row.locationId,
      lotId: row.lotId,
      handlingUnitId: row.handlingUnitId,
      quantity: qty,
    },
  }
  amountVisible.value = false
}

function removeSelection() {
  const row = amountTarget.value
  if (!row) return
  const next = { ...selected.value }
  delete next[row.key]
  selected.value = next
  amountVisible.value = false
}

function save() {
  if (!props.line || !canSave.value) return
  serverError.value = null
  modify.mutate(
    {
      lineId: props.line.id,
      allocations: Object.values(selected.value).map((e) => ({
        locationId: e.locationId,
        lotId: e.lotId,
        quantity: e.quantity,
        handlingUnitId: e.handlingUnitId,
      })),
    },
    {
      onSuccess: () => {
        toast.add({ severity: 'success', summary: 'Pick locations updated', life: 3000 })
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
  <Dialog v-model:visible="visible" modal header="Edit locations" :style="{ width: '50rem' }">
    <div v-if="line" class="flex flex-col gap-4">
      <div class="text-sm text-surface-600">
        <span class="font-medium text-surface-900">{{ line.product.sku }}</span>
        — {{ line.product.name }} · Requested {{ required }}
      </div>

      <Message v-if="serverError" severity="error" :closable="false">{{ serverError }}</Message>

      <div v-if="inventoryLoading" class="flex justify-center py-8">
        <ProgressSpinner style="width: 2.5rem; height: 2.5rem" />
      </div>

      <Message v-else-if="inventoryError" severity="error" :closable="false">
        Failed to load inventory for this product.
      </Message>

      <Message v-else-if="rows.length === 0" severity="warn" :closable="false">
        No inventory exists for this product.
      </Message>

      <template v-else>
        <DataTable :value="rows" data-key="key" size="small" scrollable scroll-height="22rem">
          <Column header="Lot" style="width: 8rem">
            <template #body="{ data: row }: { data: PickableRow }">
              <span v-if="row.lot">{{ row.lot.number }}</span>
              <span v-else class="text-surface-400">—</span>
            </template>
          </Column>
          <Column header="Handling Unit" style="width: 10rem">
            <template #body="{ data: row }: { data: PickableRow }">
              <Tag
                v-if="row.handlingUnit"
                :value="row.handlingUnit.code"
                severity="info"
                icon="pi pi-inbox"
              />
              <span v-else class="text-surface-400">Loose</span>
            </template>
          </Column>
          <Column header="Location">
            <template #body="{ data: row }: { data: PickableRow }">
              <span class="font-medium text-surface-800">{{ row.location.code }}</span>
              <span class="text-xs text-surface-500"> · {{ row.location.address }}</span>
            </template>
          </Column>
          <Column header="Available" style="width: 8rem">
            <template #body="{ data: row }: { data: PickableRow }">
              {{ row.available }}
            </template>
          </Column>
          <Column header="" style="width: 11rem">
            <template #body="{ data: row }: { data: PickableRow }">
              <Button
                :label="selectedQty(row) > 0 ? `Selected: ${selectedQty(row)}` : 'Select'"
                :icon="selectedQty(row) > 0 ? 'pi pi-check' : 'pi pi-plus'"
                size="small"
                :outlined="selectedQty(row) > 0"
                :severity="selectedQty(row) > 0 ? 'success' : 'primary'"
                :disabled="row.available === 0"
                @click="openAmount(row)"
              />
            </template>
          </Column>
        </DataTable>
        <small class="text-surface-400">
          Available includes the units this line currently reserves at each source.
        </small>
      </template>
    </div>

    <template #footer>
      <span
        v-if="line"
        class="mr-auto text-sm"
        :class="matches ? 'text-green-600' : 'text-red-600'"
      >
        Selected {{ selectedTotal }} / {{ required }}
      </span>
      <Button label="Cancel" severity="secondary" text @click="visible = false" />
      <Button
        label="Save"
        icon="pi pi-check"
        :disabled="!canSave"
        :loading="modify.isPending.value"
        @click="save"
      />
    </template>
  </Dialog>

  <!-- Sub-dialog: choose how many units to take from the selected source. -->
  <Dialog v-model:visible="amountVisible" modal header="Select amount" :style="{ width: '24rem' }">
    <div v-if="amountTarget" class="flex flex-col gap-4">
      <div class="text-sm text-surface-600 rounded-lg bg-surface-50 p-3">
        <div class="font-medium text-surface-900">{{ amountTarget.location.code }}</div>
        <div>{{ amountTarget.location.address }}</div>
        <div v-if="amountTarget.lot">Lot {{ amountTarget.lot.number }}</div>
        <div v-if="amountTarget.handlingUnit">
          Handling unit {{ amountTarget.handlingUnit.code }}
        </div>
        <div class="mt-2">Available: <b>{{ amountTarget.available }}</b></div>
      </div>

      <div class="flex flex-col gap-1">
        <label for="amount-qty" class="text-sm font-medium text-surface-700">Quantity</label>
        <InputNumber
          input-id="amount-qty"
          v-model="amountQty"
          show-buttons
          :min="1"
          :max="amountTarget.available"
          :step="1"
          :use-grouping="false"
          fluid
          :invalid="!isAmountValid"
        />
        <small class="text-surface-400">Up to {{ amountTarget.available }} can be taken here.</small>
      </div>
    </div>

    <template #footer>
      <Button
        v-if="amountTarget && selectedQty(amountTarget) > 0"
        label="Remove"
        severity="danger"
        text
        class="mr-auto"
        @click="removeSelection"
      />
      <Button label="Cancel" severity="secondary" text @click="amountVisible = false" />
      <Button label="Confirm" icon="pi pi-check" :disabled="!isAmountValid" @click="confirmAmount" />
    </template>
  </Dialog>
</template>
