<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Textarea from 'primevue/textarea'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import PageHeader from '@/components/common/PageHeader.vue'
import RefreshButton from '@/components/common/RefreshButton.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import WorkflowStepper from '@/components/common/WorkflowStepper.vue'
import PickItemDialog, { type PickItem } from './PickItemDialog.vue'
import EditPickLocationsDialog from './EditPickLocationsDialog.vue'
import RunPlannerDialog from './RunPlannerDialog.vue'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/lib/date'
import {
  pickingStrategyLabel,
  pickingStrategySeverity,
  stockOutStatusSeverity,
} from '@/lib/enum-display'
import { useStockOut, useStockOutTransition, useUpdateStockOutDescription, type StockOutAction } from './useStockOuts'
import type { StockOutItemDto, StockOutLineDto } from '@/types/stock-outs'
import type { LocationRef, LotRef, ProductRef } from '@/types/refs'
import type { PickingStrategyType } from '@/types/enums'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const confirm = useConfirm()
const toast = useToast()

const id = computed(() => route.params.id as string)
const { data: stockOut, isLoading, isFetching, isError, error, refetch } = useStockOut(id)
const transition = useStockOutTransition()
const updateDescription = useUpdateStockOutDescription(id.value)

const editingDescription = ref(false)
const descriptionDraft = ref('')

function openDescriptionEdit() {
  descriptionDraft.value = stockOut.value?.description ?? ''
  editingDescription.value = true
}

function saveDescription() {
  updateDescription.mutate(descriptionDraft.value.trim() || null, {
    onSuccess: () => { editingDescription.value = false },
    onError: (err) =>
      toast.add({ severity: 'error', summary: 'Save failed', detail: err.message, life: 6000 }),
  })
}

const steps = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Picking', label: 'Picking' },
  { value: 'Completed', label: 'Completed' },
]

const isCancelled = computed(() => stockOut.value?.status === 'Cancelled')
// When cancelled we render the view of the phase it was cancelled from.
const cancelledFrom = computed(() => stockOut.value?.cancelledFrom ?? null)
const canCancel = computed(
  () =>
    auth.canMutate &&
    (stockOut.value?.status === 'Draft' || stockOut.value?.status === 'Picking'),
)
// Pick allocations can only be re-planned while the document is a Draft.
const canEditLocations = computed(
  () => auth.canMutate && stockOut.value?.status === 'Draft',
)

// Show the location-ordered pick path while picking, and also for a stock-out
// cancelled during Picking (read-only) so the prior phase is preserved.
const showPickLayout = computed(
  () =>
    stockOut.value?.status === 'Picking' ||
    (stockOut.value?.status === 'Cancelled' && cancelledFrom.value === 'Picking'),
)
// No picking actions once the document is cancelled.
const readonlyPick = computed(() => stockOut.value?.status === 'Cancelled')

// Every item of every line fully picked — the gate for completing.
const allPicked = computed(
  () =>
    !!stockOut.value &&
    stockOut.value.lines.every((l) => l.items.every((i) => i.pickedQuantity >= i.quantity)),
)

/** One thing to pick at a stop on the path. */
interface PickPathItem {
  id: string
  product: ProductRef
  lot: LotRef | null
  quantity: number
  pickedQuantity: number
  strategy: PickingStrategyType
}

/** A stop on the pick walk: one location and everything to take there. */
interface PickPathStop {
  location: LocationRef
  items: PickPathItem[]
}

// Ordinal string compare — matches the backend's string.CompareOrdinal on addresses.
function ordinalCompare(a: string, b: string): number {
  return a < b ? -1 : a > b ? 1 : 0
}

// While picking, present items as a location-ordered path so the worker walks
// address-ascending and sees everything to take at each stop. Empty otherwise.
const pickPath = computed<PickPathStop[]>(() => {
  const data = stockOut.value
  if (!data || !showPickLayout.value) return []

  const byLocation = new Map<string, PickPathStop>()
  for (const line of data.lines) {
    for (const item of line.items) {
      let stop = byLocation.get(item.location.id)
      if (!stop) {
        stop = { location: item.location, items: [] }
        byLocation.set(item.location.id, stop)
      }
      stop.items.push({
        id: item.id,
        product: line.product,
        lot: item.lot,
        quantity: item.quantity,
        pickedQuantity: item.pickedQuantity,
        strategy: item.strategy,
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

/** Icon + colour describing how far an item has been picked. */
function pickProgress(item: PickPathItem) {
  if (item.pickedQuantity >= item.quantity)
    return { icon: 'pi pi-check-circle', class: 'text-green-500' }
  if (item.pickedQuantity > 0)
    return { icon: 'pi pi-exclamation-circle', class: 'text-yellow-500' }
  return { icon: 'pi pi-circle', class: 'text-surface-300' }
}

const editLocationsVisible = ref(false)
const editLocationsLine = ref<StockOutLineDto | null>(null)

function openEditLocations(line: StockOutLineDto) {
  editLocationsLine.value = line
  editLocationsVisible.value = true
}

const runPlannerVisible = ref(false)
const runPlannerLine = ref<StockOutLineDto | null>(null)

function openRunPlanner(line: StockOutLineDto) {
  runPlannerLine.value = line
  runPlannerVisible.value = true
}

const pickVisible = ref(false)
const pickTarget = ref<PickItem | null>(null)

function openPick(item: PickPathItem, location: LocationRef) {
  pickTarget.value = {
    id: item.id,
    product: item.product,
    lot: item.lot,
    location,
    quantity: item.quantity,
    pickedQuantity: item.pickedQuantity,
  }
  pickVisible.value = true
}

interface ActionOptions {
  header: string
  message: string
  success: string
  danger?: boolean
}

function runAction(action: StockOutAction, opts: ActionOptions) {
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

function startPicking() {
  runAction('startPicking', {
    header: 'Start picking',
    message: 'Move this stock-out into Picking?',
    success: 'Picking started',
  })
}

function complete() {
  runAction('complete', {
    header: 'Complete stock-out',
    message: 'Mark this stock-out as completed?',
    success: 'Stock-out completed',
  })
}

function cancel() {
  runAction('cancel', {
    header: 'Cancel stock-out',
    message: 'Cancel this stock-out? This cannot be undone.',
    success: 'Stock-out cancelled',
    danger: true,
  })
}
</script>

<template>
  <section class="p-6 flex flex-col gap-6" style="max-width: 1100px">
    <PageHeader title="Stock-Out" :subtitle="stockOut ? formatDateTime(stockOut.createdAt) : ''">
      <template #title-actions>
        <RefreshButton :loading="isFetching" @click="() => refetch()" />
      </template>
      <template #actions>
        <Button
          label="Back"
          icon="pi pi-arrow-left"
          severity="secondary"
          text
          @click="router.push({ name: 'stock-outs' })"
        />
        <template v-if="stockOut">
          <Button
            v-if="stockOut.status === 'Draft'"
            label="Start picking"
            icon="pi pi-play"
            :loading="transition.isPending.value"
            @click="startPicking"
          />
          <Button
            v-else-if="stockOut.status === 'Picking'"
            label="Complete"
            icon="pi pi-flag"
            :disabled="!allPicked"
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
      {{ error?.message ?? 'Failed to load stock-out.' }}
    </Message>

    <template v-else-if="stockOut">
      <div class="rounded-xl border border-surface-200 bg-white p-6 flex flex-col gap-5">
        <div class="flex items-center gap-3">
          <span class="text-sm text-surface-500">Status</span>
          <StatusBadge :value="stockOut.status" :severity="stockOutStatusSeverity[stockOut.status]" />
          <span v-if="isCancelled && cancelledFrom" class="text-sm text-surface-500">
            (cancelled during {{ cancelledFrom }})
          </span>
          <span v-if="stockOut.createdBy" class="text-sm text-surface-500 ml-auto">
            Created by {{ stockOut.createdBy }}
          </span>
        </div>
        <WorkflowStepper :steps="steps" :current="stockOut.status" :cancelled="isCancelled" />
        <p v-if="stockOut.status === 'Picking' && !allPicked" class="text-xs text-surface-400">
          Pick every item to enable completing this stock-out.
        </p>

        <div class="flex flex-col gap-1">
          <div class="flex items-center gap-2">
            <span class="text-xs text-surface-500">Description</span>
            <Button
              v-if="auth.canMutate && !editingDescription"
              icon="pi pi-pencil"
              size="small"
              text
              rounded
              aria-label="Edit description"
              @click="openDescriptionEdit"
            />
          </div>
          <template v-if="editingDescription">
            <Textarea
              v-model="descriptionDraft"
              :maxlength="500"
              rows="2"
              autoResize
              fluid
              class="text-sm"
            />
            <div class="flex gap-2">
              <Button
                label="Save"
                size="small"
                icon="pi pi-check"
                :loading="updateDescription.isPending.value"
                @click="saveDescription"
              />
              <Button
                label="Cancel"
                size="small"
                severity="secondary"
                text
                @click="editingDescription = false"
              />
            </div>
          </template>
          <p v-else class="text-sm text-surface-700 whitespace-pre-wrap">
            {{ stockOut.description ?? '—' }}
          </p>
        </div>
      </div>

      <!-- Picking: location-ordered path for the worker's walk, with per-row progress. -->
      <div v-if="showPickLayout" class="flex flex-col gap-4">
        <div
          v-for="(stop, index) in pickPath"
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
              <template #body="{ data: item }: { data: PickPathItem }">
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
              <template #body="{ data: item }: { data: PickPathItem }">
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
            <Column header="Pick" style="width: 13rem">
              <template #body="{ data: item }: { data: PickPathItem }">
                <div class="flex items-center gap-2">
                  <i :class="[pickProgress(item).icon, pickProgress(item).class]" />
                  <span>{{ item.pickedQuantity }} / {{ item.quantity }}</span>
                  <span v-if="item.pickedQuantity < item.quantity" class="text-xs text-surface-400">
                    · {{ item.quantity - item.pickedQuantity }} left
                  </span>
                </div>
              </template>
            </Column>
            <Column header="Strategy" style="width: 12rem">
              <template #body="{ data: item }: { data: PickPathItem }">
                <StatusBadge
                  :value="pickingStrategyLabel[item.strategy]"
                  :severity="pickingStrategySeverity[item.strategy]"
                />
              </template>
            </Column>
            <Column header="" style="width: 9rem">
              <template #body="{ data: item }: { data: PickPathItem }">
                <Button
                  v-if="!readonlyPick && item.pickedQuantity < item.quantity"
                  label="Pick"
                  icon="pi pi-arrow-up"
                  size="small"
                  @click="openPick(item, stop.location)"
                />
                <span
                  v-else-if="item.pickedQuantity >= item.quantity"
                  class="text-green-600 text-sm inline-flex items-center gap-1"
                >
                  <i class="pi pi-check" /> Picked
                </span>
              </template>
            </Column>
          </DataTable>
        </div>
      </div>

      <!-- Other statuses: per-line items (read-only). -->
      <div v-else class="flex flex-col gap-4">
        <div
          v-for="line in stockOut.lines"
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
              <div class="text-xs text-surface-500 mt-0.5">Quantity: {{ line.quantity }}</div>
            </div>
            <div class="flex items-center gap-2">
              <Button
                v-if="canEditLocations"
                label="Edit locations"
                icon="pi pi-pencil"
                size="small"
                outlined
                @click="openEditLocations(line)"
              />
              <Button
                v-if="canEditLocations"
                label="Run planner"
                icon="pi pi-bolt"
                size="small"
                outlined
                severity="secondary"
                @click="openRunPlanner(line)"
              />
              <StatusBadge
                :value="pickingStrategyLabel[line.strategy]"
                :severity="pickingStrategySeverity[line.strategy]"
              />
            </div>
          </div>

          <DataTable :value="line.items" data-key="id">
            <Column header="Location">
              <template #body="{ data: item }: { data: StockOutItemDto }">
                <RouterLink
                  :to="{ name: 'location-detail', params: { id: item.location.id } }"
                  class="text-primary-600 hover:underline"
                >
                  {{ item.location.code }}
                </RouterLink>
                <span class="text-xs text-surface-500"> · {{ item.location.address }}</span>
              </template>
            </Column>
            <Column header="Lot" style="width: 12rem">
              <template #body="{ data: item }: { data: StockOutItemDto }">
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
            <Column field="quantity" header="Quantity" style="width: 9rem" />
            <Column header="Picked" style="width: 9rem">
              <template #body="{ data: item }: { data: StockOutItemDto }">
                {{ item.pickedQuantity }} / {{ item.quantity }}
              </template>
            </Column>
          </DataTable>
        </div>
      </div>
    </template>

    <EditPickLocationsDialog
      v-model:visible="editLocationsVisible"
      :stock-out-id="id"
      :line="editLocationsLine"
    />

    <RunPlannerDialog
      v-model:visible="runPlannerVisible"
      :stock-out-id="id"
      :line="runPlannerLine"
    />

    <PickItemDialog v-model:visible="pickVisible" :stock-out-id="id" :item="pickTarget" />
  </section>
</template>
