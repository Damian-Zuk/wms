<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import PageHeader from '@/components/common/PageHeader.vue'
import RefreshButton from '@/components/common/RefreshButton.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import MoveHandlingUnitDialog from './MoveHandlingUnitDialog.vue'
import PackUnpackHandlingUnitDialog from './PackUnpackHandlingUnitDialog.vue'
import { useDeleteHandlingUnit, useHandlingUnit } from './useHandlingUnits'
import { handlingUnitTypeLabel, handlingUnitTypeSeverity } from '@/lib/enum-display'
import { useAuthStore } from '@/stores/auth'
import { formatDate } from '@/lib/date'
import type { HandlingUnitContentDto } from '@/types/handling-units'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const confirm = useConfirm()
const toast = useToast()

const id = computed(() => route.params.id as string)
const { data: unit, isLoading, isFetching, isError, error, refetch } = useHandlingUnit(id)
const del = useDeleteHandlingUnit()

const moveVisible = ref(false)
const packVisible = ref(false)
const packMode = ref<'pack' | 'unpack'>('pack')

const totalOnHand = computed(() =>
  (unit.value?.contents ?? []).reduce((sum, c) => sum + c.onHand, 0),
)

const isEmpty = computed(() => totalOnHand.value === 0)

function openPack(mode: 'pack' | 'unpack') {
  packMode.value = mode
  packVisible.value = true
}

function viewMovements() {
  router.push({ name: 'stock-movements', query: { handlingUnitId: id.value } })
}

function onDelete() {
  confirm.require({
    message: 'Delete this handling unit? Only empty units can be deleted.',
    header: 'Confirm delete',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', text: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: () => {
      del.mutate(id.value, {
        onSuccess: () => {
          toast.add({ severity: 'success', summary: 'Handling unit deleted', life: 3000 })
          router.push({ name: 'handling-units' })
        },
        onError: (err) => {
          toast.add({
            severity: 'error',
            summary: 'Delete failed',
            detail: err.message,
            life: 5000,
          })
        },
      })
    },
  })
}
</script>

<template>
  <section class="p-6 flex flex-col gap-6" style="max-width: 1100px">
    <PageHeader
      :title="unit ? unit.code : 'Handling Unit'"
      :subtitle="unit ? handlingUnitTypeLabel[unit.type] : ''"
    >
      <template #title-actions>
        <RefreshButton :loading="isFetching" @click="() => refetch()" />
      </template>
      <template #actions>
        <Button
          label="Back"
          icon="pi pi-arrow-left"
          severity="secondary"
          text
          @click="router.push({ name: 'handling-units' })"
        />
        <template v-if="auth.canMutate && unit">
          <Button
            v-if="unit.location"
            label="Move"
            icon="pi pi-arrows-alt"
            @click="moveVisible = true"
          />
          <Button
            v-if="unit.location"
            label="Pack"
            icon="pi pi-download"
            severity="secondary"
            outlined
            @click="openPack('pack')"
          />
          <Button
            v-if="unit.location"
            label="Unpack"
            icon="pi pi-upload"
            severity="secondary"
            outlined
            :disabled="isEmpty"
            @click="openPack('unpack')"
          />
          <Button
            label="Delete"
            icon="pi pi-trash"
            severity="danger"
            outlined
            :disabled="!isEmpty"
            :loading="del.isPending.value"
            @click="onDelete"
          />
        </template>
      </template>
    </PageHeader>

    <div v-if="isLoading" class="flex justify-center py-10">
      <ProgressSpinner />
    </div>

    <Message v-else-if="isError" severity="error" :closable="false">
      {{ error?.message ?? 'Failed to load handling unit.' }}
    </Message>

    <template v-else-if="unit">
      <dl
        class="grid grid-cols-[12rem_1fr] gap-y-4 gap-x-4 rounded-xl border border-surface-200 bg-white p-6"
      >
        <dt class="text-surface-500">Code</dt>
        <dd class="text-surface-900 font-medium">{{ unit.code }}</dd>

        <dt class="text-surface-500">Type</dt>
        <dd>
          <StatusBadge
            :value="handlingUnitTypeLabel[unit.type]"
            :severity="handlingUnitTypeSeverity[unit.type]"
          />
        </dd>

        <dt class="text-surface-500">Location</dt>
        <dd class="text-surface-900">
          <template v-if="unit.location">
            <RouterLink
              :to="{ name: 'location-detail', params: { id: unit.location.id } }"
              class="text-primary-600 hover:underline"
            >
              {{ unit.location.code }} · {{ unit.location.address }}
            </RouterLink>
          </template>
          <StatusBadge v-else value="Expected (not yet put away)" severity="secondary" />
        </dd>

        <dt class="text-surface-500">Total on hand</dt>
        <dd class="text-surface-900">{{ totalOnHand }}</dd>

        <dt class="text-surface-500">Created</dt>
        <dd class="text-surface-900">
          {{ formatDate(unit.createdAt) }}
          <span v-if="unit.createdBy" class="text-surface-500">by {{ unit.createdBy }}</span>
        </dd>
      </dl>

      <section class="flex flex-col gap-3">
        <h2 class="text-lg font-semibold text-surface-900">Contents</h2>
        <DataTable :value="unit.contents" data-key="inventoryId" class="rounded-xl border border-surface-200">
          <template #empty>
            <div class="text-surface-500 p-3">This unit is empty.</div>
          </template>
          <Column header="Product">
            <template #body="{ data: row }: { data: HandlingUnitContentDto }">
              {{ row.product.sku }} — {{ row.product.name }}
            </template>
          </Column>
          <Column header="Lot" style="width: 12rem">
            <template #body="{ data: row }: { data: HandlingUnitContentDto }">
              {{ row.lot?.number ?? '—' }}
            </template>
          </Column>
          <Column header="On Hand" style="width: 8rem">
            <template #body="{ data: row }: { data: HandlingUnitContentDto }">
              {{ row.onHand }}
            </template>
          </Column>
          <Column header="Reserved" style="width: 8rem">
            <template #body="{ data: row }: { data: HandlingUnitContentDto }">
              <span :class="row.reserved > 0 ? 'text-amber-600 font-medium' : ''">
                {{ row.reserved }}
              </span>
            </template>
          </Column>
          <Column header="Available" style="width: 8rem">
            <template #body="{ data: row }: { data: HandlingUnitContentDto }">
              {{ row.available }}
            </template>
          </Column>
          <Column header="Received" style="width: 11rem">
            <template #body="{ data: row }: { data: HandlingUnitContentDto }">
              {{ formatDate(row.receivedAt) }}
            </template>
          </Column>
        </DataTable>
      </section>

      <Button
        label="View movements"
        icon="pi pi-history"
        severity="secondary"
        outlined
        fluid
        @click="viewMovements"
      />

      <MoveHandlingUnitDialog v-model:visible="moveVisible" :unit="unit" />
      <PackUnpackHandlingUnitDialog
        v-model:visible="packVisible"
        :unit="unit"
        :mode="packMode"
      />
    </template>
  </section>
</template>
