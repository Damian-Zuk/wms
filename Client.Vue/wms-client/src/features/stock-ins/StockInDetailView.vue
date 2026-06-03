<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import PageHeader from '@/components/common/PageHeader.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import WorkflowStepper from '@/components/common/WorkflowStepper.vue'
import ModifyPlacementsDialog from './ModifyPlacementsDialog.vue'
import PutawayItemDialog, { type PutawayItem } from './PutawayItemDialog.vue'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/lib/date'
import {
  putawayStrategyLabel,
  putawayStrategySeverity,
  stockInStatusSeverity,
} from '@/lib/enum-display'
import { useStockIn, useStockInTransition, type StockInAction } from './useStockIns'
import type { StockInLineDto, StockInPlacementDto } from '@/types/stock-ins'
import type { LocationRef, LotRef, ProductRef } from '@/types/refs'
import type { PutawayStrategyType } from '@/types/enums'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const confirm = useConfirm()
const toast = useToast()

const id = computed(() => route.params.id as string)
const { data: stockIn, isLoading, isError, error } = useStockIn(id)
const transition = useStockInTransition()

const steps = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Putaway', label: 'Putaway' },
  { value: 'Completed', label: 'Completed' },
]

const isCancelled = computed(() => stockIn.value?.status === 'Cancelled')
// When cancelled we render the view of the phase it was cancelled from.
const cancelledFrom = computed(() => stockIn.value?.cancelledFrom ?? null)
const canCancel = computed(
  () =>
    auth.canMutate &&
    (stockIn.value?.status === 'Draft' || stockIn.value?.status === 'Putaway'),
)
// Placements can only be re-planned while the document is a Draft.
const canEditPlacements = computed(
  () => auth.canMutate && stockIn.value?.status === 'Draft',
)

// Show the location-ordered putaway path while putting away, and also for a
// stock-in cancelled during Putaway (read-only) so the prior phase is preserved.
const showPutawayLayout = computed(
  () =>
    stockIn.value?.status === 'Putaway' ||
    (stockIn.value?.status === 'Cancelled' && cancelledFrom.value === 'Putaway'),
)
// No putaway actions once the document is cancelled.
const readonlyPutaway = computed(() => stockIn.value?.status === 'Cancelled')

// Every placement of every line fully put away — the gate for completing.
const allPlaced = computed(
  () =>
    !!stockIn.value &&
    stockIn.value.lines.every((l) => l.placements.every((p) => p.placedQuantity >= p.quantity)),
)

/** One thing to put away at a stop on the path. */
interface PutawayPathItem {
  id: string
  product: ProductRef
  lot: LotRef | null
  quantity: number
  placedQuantity: number
  strategy: PutawayStrategyType
}

/** A stop on the putaway walk: one location and everything that goes there. */
interface PutawayPathStop {
  location: LocationRef
  items: PutawayPathItem[]
}

// Ordinal string compare — matches the backend's string.CompareOrdinal on addresses.
function ordinalCompare(a: string, b: string): number {
  return a < b ? -1 : a > b ? 1 : 0
}

// While putting away, present placements as a location-ordered path so the worker
// walks address-ascending and sees everything to drop at each stop. Empty otherwise.
const putawayPath = computed<PutawayPathStop[]>(() => {
  const data = stockIn.value
  if (!data || !showPutawayLayout.value) return []

  const byLocation = new Map<string, PutawayPathStop>()
  for (const line of data.lines) {
    for (const p of line.placements) {
      let stop = byLocation.get(p.location.id)
      if (!stop) {
        stop = { location: p.location, items: [] }
        byLocation.set(p.location.id, stop)
      }
      stop.items.push({
        id: p.id,
        product: line.product,
        lot: line.lot,
        quantity: p.quantity,
        placedQuantity: p.placedQuantity,
        strategy: p.strategy,
      })
    }
  }

  const stops = [...byLocation.values()]
  // Walk order: location address ascending (ordinal, matching the backend).
  stops.sort((a, b) => ordinalCompare(a.location.address, b.location.address))
  // Within a stop, group by product SKU, then lot, then strategy for a stable read.
  for (const stop of stops) {
    stop.items.sort(
      (a, b) =>
        ordinalCompare(a.product.sku, b.product.sku) ||
        ordinalCompare(a.lot?.number ?? '', b.lot?.number ?? '') ||
        ordinalCompare(a.strategy, b.strategy),
    )
  }
  return stops
})

/** Icon + colour describing how far a placement has been put away. */
function placementProgress(item: PutawayPathItem) {
  if (item.placedQuantity >= item.quantity)
    return { icon: 'pi pi-check-circle', class: 'text-green-500' }
  if (item.placedQuantity > 0)
    return { icon: 'pi pi-exclamation-circle', class: 'text-yellow-500' }
  return { icon: 'pi pi-circle', class: 'text-surface-300' }
}

const editVisible = ref(false)
const editLine = ref<StockInLineDto | null>(null)

function openEdit(line: StockInLineDto) {
  editLine.value = line
  editVisible.value = true
}

const putawayVisible = ref(false)
const putawayTarget = ref<PutawayItem | null>(null)

function openPutaway(item: PutawayPathItem, location: LocationRef) {
  putawayTarget.value = {
    id: item.id,
    product: item.product,
    lot: item.lot,
    location,
    quantity: item.quantity,
    placedQuantity: item.placedQuantity,
  }
  putawayVisible.value = true
}

interface ActionOptions {
  header: string
  message: string
  success: string
  danger?: boolean
}

function runAction(action: StockInAction, opts: ActionOptions) {
  confirm.require({
    header: opts.header,
    message: opts.message,
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Back', severity: 'secondary', text: true },
    acceptProps: { label: 'Confirm', severity: opts.danger ? 'danger' : 'primary' },
    accept: () => {
      transition.mutate(
        { id: id.value, action },
        {
          onSuccess: () => toast.add({ severity: 'success', summary: opts.success, life: 3000 }),
          onError: (err) =>
            toast.add({
              severity: 'error',
              summary: 'Action failed',
              detail: err.message,
              life: 6000,
            }),
        },
      )
    },
  })
}

function startPutaway() {
  runAction('startPutaway', {
    header: 'Start putaway',
    message: 'Move this stock-in into Putaway?',
    success: 'Putaway started',
  })
}

function complete() {
  runAction('complete', {
    header: 'Complete stock-in',
    message: 'Mark this stock-in as completed?',
    success: 'Stock-in completed',
  })
}

function cancel() {
  runAction('cancel', {
    header: 'Cancel stock-in',
    message: 'Cancel this stock-in? This cannot be undone.',
    success: 'Stock-in cancelled',
    danger: true,
  })
}
</script>

<template>
  <section class="p-6 flex flex-col gap-6" style="max-width: 1100px">
    <PageHeader title="Stock-In" :subtitle="stockIn ? formatDateTime(stockIn.createdAt) : ''">
      <template #actions>
        <Button
          label="Back"
          icon="pi pi-arrow-left"
          severity="secondary"
          text
          @click="router.push({ name: 'stock-ins' })"
        />
        <template v-if="stockIn">
          <Button
            v-if="stockIn.status === 'Draft'"
            label="Start putaway"
            icon="pi pi-play"
            :loading="transition.isPending.value"
            @click="startPutaway"
          />
          <Button
            v-else-if="stockIn.status === 'Putaway'"
            label="Complete"
            icon="pi pi-flag"
            :disabled="!allPlaced"
            :loading="transition.isPending.value"
            @click="complete"
          />
          <Button
            v-if="canCancel"
            label="Cancel"
            icon="pi pi-times"
            severity="danger"
            outlined
            :loading="transition.isPending.value"
            @click="cancel"
          />
        </template>
      </template>
    </PageHeader>

    <div v-if="isLoading" class="flex justify-center py-10">
      <ProgressSpinner />
    </div>

    <Message v-else-if="isError" severity="error" :closable="false">
      {{ error?.message ?? 'Failed to load stock-in.' }}
    </Message>

    <template v-else-if="stockIn">
      <div class="rounded-xl border border-surface-200 bg-white p-6 flex flex-col gap-5">
        <div class="flex items-center gap-3">
          <span class="text-sm text-surface-500">Status</span>
          <StatusBadge :value="stockIn.status" :severity="stockInStatusSeverity[stockIn.status]" />
          <span v-if="isCancelled && cancelledFrom" class="text-sm text-surface-500">
            (cancelled during {{ cancelledFrom }})
          </span>
          <span v-if="stockIn.createdBy" class="text-sm text-surface-500 ml-auto">
            Created by {{ stockIn.createdBy }}
          </span>
        </div>
        <WorkflowStepper :steps="steps" :current="stockIn.status" :cancelled="isCancelled" />
        <p v-if="stockIn.status === 'Putaway' && !allPlaced" class="text-xs text-surface-400">
          Put away every item to enable completing this stock-in.
        </p>
        <p v-if="stockIn.modifiedBy" class="text-xs text-surface-400">
          Placements last edited by {{ stockIn.modifiedBy }}
          <span v-if="stockIn.modifiedAt"> on {{ formatDateTime(stockIn.modifiedAt) }}</span>
        </p>
      </div>

      <!-- Putaway: location-ordered path for the worker's walk, with per-row progress. -->
      <div v-if="showPutawayLayout" class="flex flex-col gap-4">
        <div
          v-for="(stop, index) in putawayPath"
          :key="stop.location.id"
          class="rounded-xl border border-surface-200 bg-white p-5 flex flex-col gap-3"
        >
          <div class="flex items-center gap-3">
            <span
              class="flex items-center justify-center w-7 h-7 rounded-full bg-primary-50 text-primary-600 text-sm font-medium"
            >
              {{ index + 1 }}
            </span>
            <div>
              <RouterLink
                :to="{ name: 'location-detail', params: { id: stop.location.id } }"
                class="text-primary-600 hover:underline font-medium"
              >
                {{ stop.location.code }}
              </RouterLink>
              <span class="text-xs text-surface-500"> · {{ stop.location.address }}</span>
            </div>
          </div>

          <DataTable :value="stop.items" data-key="id">
            <Column header="Product">
              <template #body="{ data: item }: { data: PutawayPathItem }">
                <RouterLink
                  :to="{ name: 'product-detail', params: { id: item.product.id } }"
                  class="text-primary-600 hover:underline"
                >
                  {{ item.product.sku }}
                </RouterLink>
                <span class="text-surface-700"> — {{ item.product.name }}</span>
              </template>
            </Column>
            <Column header="Lot" style="width: 10rem">
              <template #body="{ data: item }: { data: PutawayPathItem }">
                <RouterLink
                  v-if="item.lot"
                  :to="{ name: 'lot-detail', params: { id: item.lot.id } }"
                  class="text-primary-600 hover:underline"
                >
                  {{ item.lot.number }}
                </RouterLink>
                <span v-else class="text-surface-400">—</span>
              </template>
            </Column>
            <Column header="Putaway" style="width: 13rem">
              <template #body="{ data: item }: { data: PutawayPathItem }">
                <div class="flex items-center gap-2">
                  <i :class="[placementProgress(item).icon, placementProgress(item).class]" />
                  <span>{{ item.placedQuantity }} / {{ item.quantity }}</span>
                  <span v-if="item.placedQuantity < item.quantity" class="text-xs text-surface-400">
                    · {{ item.quantity - item.placedQuantity }} left
                  </span>
                </div>
              </template>
            </Column>
            <Column header="Strategy" style="width: 10rem">
              <template #body="{ data: item }: { data: PutawayPathItem }">
                <StatusBadge
                  :value="putawayStrategyLabel[item.strategy]"
                  :severity="putawayStrategySeverity[item.strategy]"
                />
              </template>
            </Column>
            <Column header="" style="width: 9rem">
              <template #body="{ data: item }: { data: PutawayPathItem }">
                <Button
                  v-if="!readonlyPutaway && item.placedQuantity < item.quantity"
                  label="Put away"
                  icon="pi pi-arrow-down"
                  size="small"
                  @click="openPutaway(item, stop.location)"
                />
                <span
                  v-else-if="item.placedQuantity >= item.quantity"
                  class="text-green-600 text-sm inline-flex items-center gap-1"
                >
                  <i class="pi pi-check" /> Placed
                </span>
              </template>
            </Column>
          </DataTable>
        </div>
      </div>

      <!-- Other statuses: per-line placements (editable while Draft). -->
      <div v-else class="flex flex-col gap-4">
        <div
          v-for="line in stockIn.lines"
          :key="line.id"
          class="rounded-xl border border-surface-200 bg-white p-5 flex flex-col gap-3"
        >
          <div class="flex items-start justify-between gap-4">
            <div>
              <RouterLink
                :to="{ name: 'product-detail', params: { id: line.product.id } }"
                class="text-primary-600 hover:underline font-medium"
              >
                {{ line.product.sku }}
              </RouterLink>
              <span class="text-surface-700"> — {{ line.product.name }}</span>
              <div class="text-xs text-surface-500 mt-0.5">
                Lot:
                <RouterLink
                  v-if="line.lot"
                  :to="{ name: 'lot-detail', params: { id: line.lot.id } }"
                  class="text-primary-600 hover:underline"
                >
                  {{ line.lot.number }}
                </RouterLink>
                <span v-else>—</span>
                · Quantity: {{ line.quantity }}
              </div>
            </div>
            <Button
              v-if="canEditPlacements"
              label="Edit placements"
              icon="pi pi-pencil"
              size="small"
              outlined
              @click="openEdit(line)"
            />
          </div>

          <DataTable :value="line.placements" data-key="id">
            <Column header="Location">
              <template #body="{ data: p }: { data: StockInPlacementDto }">
                <RouterLink
                  :to="{ name: 'location-detail', params: { id: p.location.id } }"
                  class="text-primary-600 hover:underline"
                >
                  {{ p.location.code }}
                </RouterLink>
                <span class="text-xs text-surface-500"> · {{ p.location.address }}</span>
              </template>
            </Column>
            <Column field="quantity" header="Quantity" style="width: 9rem" />
            <Column header="Strategy" style="width: 12rem">
              <template #body="{ data: p }: { data: StockInPlacementDto }">
                <StatusBadge
                  :value="putawayStrategyLabel[p.strategy]"
                  :severity="putawayStrategySeverity[p.strategy]"
                />
              </template>
            </Column>
          </DataTable>
        </div>
      </div>
    </template>

    <ModifyPlacementsDialog
      v-model:visible="editVisible"
      :stock-in-id="id"
      :line="editLine"
    />

    <PutawayItemDialog
      v-model:visible="putawayVisible"
      :stock-in-id="id"
      :item="putawayTarget"
    />
  </section>
</template>
