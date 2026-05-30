<script setup lang="ts">
import { computed } from 'vue'
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
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/lib/date'
import { stockInStatusSeverity } from '@/lib/enum-display'
import { useStockIn, useStockInTransition, type StockInAction } from './useStockIns'
import type { StockInItemDto } from '@/types/stock-ins'

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
  { value: 'Receiving', label: 'Receiving' },
  { value: 'Received', label: 'Received' },
  { value: 'Completed', label: 'Completed' },
]

const isCancelled = computed(() => stockIn.value?.status === 'Cancelled')
const canCancel = computed(
  () =>
    auth.canMutate &&
    (stockIn.value?.status === 'Draft' || stockIn.value?.status === 'Receiving'),
)

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

function startReceiving() {
  runAction('startReceiving', {
    header: 'Start receiving',
    message: 'Move this stock-in into Receiving?',
    success: 'Receiving started',
  })
}

function receive() {
  runAction('receive', {
    header: 'Receive stock',
    message: 'This books the items into inventory and cannot be undone. Continue?',
    success: 'Stock received',
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
            label="Start receiving"
            icon="pi pi-play"
            :loading="transition.isPending.value"
            @click="startReceiving"
          />
          <Button
            v-else-if="stockIn.status === 'Receiving'"
            label="Receive"
            icon="pi pi-check"
            :loading="transition.isPending.value"
            @click="receive"
          />
          <Button
            v-else-if="stockIn.status === 'Received'"
            label="Complete"
            icon="pi pi-flag"
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
          <span v-if="stockIn.createdBy" class="text-sm text-surface-500 ml-auto">
            Created by {{ stockIn.createdBy }}
          </span>
        </div>
        <WorkflowStepper :steps="steps" :current="stockIn.status" :cancelled="isCancelled" />
      </div>

      <DataTable :value="stockIn.items" data-key="id" class="rounded-xl overflow-hidden">
        <Column header="Product">
          <template #body="{ data: item }: { data: StockInItemDto }">
            <RouterLink
              :to="{ name: 'product-detail', params: { id: item.product.id } }"
              class="text-primary-600 hover:underline"
            >
              {{ item.product.sku }}
            </RouterLink>
            <div class="text-xs text-surface-500">{{ item.product.name }}</div>
          </template>
        </Column>
        <Column header="Location">
          <template #body="{ data: item }: { data: StockInItemDto }">
            <RouterLink
              :to="{ name: 'location-detail', params: { id: item.location.id } }"
              class="text-primary-600 hover:underline"
            >
              {{ item.location.code }}
            </RouterLink>
            <div class="text-xs text-surface-500">{{ item.location.address }}</div>
          </template>
        </Column>
        <Column header="Lot" style="width: 12rem">
          <template #body="{ data: item }: { data: StockInItemDto }">
            <RouterLink
              v-if="item.lot"
              :to="{ name: 'lot-detail', params: { id: item.lot.id } }"
              class="text-primary-600 hover:underline"
            >
              {{ item.lot.number }}
            </RouterLink>
            <span v-else>—</span>
          </template>
        </Column>
        <Column field="quantity" header="Quantity" style="width: 9rem" />
      </DataTable>
    </template>
  </section>
</template>
