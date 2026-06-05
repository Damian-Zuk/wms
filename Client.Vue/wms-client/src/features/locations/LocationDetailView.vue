<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import PageHeader from '@/components/common/PageHeader.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import { useAuthStore } from '@/stores/auth'
import { locationTypeSeverity, temperatureZoneSeverity } from '@/lib/enum-display'
import { useDeleteLocation, useLocation } from './useLocations'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const confirm = useConfirm()
const toast = useToast()

const id = computed(() => route.params.id as string)
const { data: location, isLoading, isError, error } = useLocation(id)
const del = useDeleteLocation()

/** Occupancy utilisation as a 0–100 percentage, or null when that dimension is unlimited. */
function utilisationPercent(used: number, limit: number | null): number | null {
  if (limit == null || limit === 0) return null
  return Math.round((used / limit) * 100)
}

function onEdit() {
  router.push({ name: 'location-edit', params: { id: id.value } })
}

function onDelete() {
  confirm.require({
    message: 'Delete this location? This cannot be undone.',
    header: 'Confirm delete',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', text: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: () => {
      del.mutate(id.value, {
        onSuccess: () => {
          toast.add({ severity: 'success', summary: 'Location deleted', life: 3000 })
          router.push({ name: 'locations' })
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
  <section class="p-6 flex flex-col gap-6" style="max-width: 900px">
    <PageHeader :title="location?.code ?? 'Location'" :subtitle="location?.display">
      <template #actions>
        <Button
          label="Back"
          icon="pi pi-arrow-left"
          severity="secondary"
          text
          @click="router.push({ name: 'locations' })"
        />
        <template v-if="auth.canMutate">
          <Button label="Edit" icon="pi pi-pencil" @click="onEdit" />
          <Button
            label="Delete"
            icon="pi pi-trash"
            severity="danger"
            outlined
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
      {{ error?.message ?? 'Failed to load location.' }}
    </Message>

    <dl
      v-else-if="location"
      class="grid grid-cols-[12rem_1fr] gap-y-4 gap-x-4 rounded-xl border border-surface-200 bg-white p-6"
    >
      <dt class="text-surface-500">Code</dt>
      <dd class="text-surface-900 font-medium">{{ location.code }}</dd>

      <dt class="text-surface-500">Address</dt>
      <dd class="text-surface-900">{{ location.display }}</dd>

      <dt class="text-surface-500">Type</dt>
      <dd><StatusBadge :value="location.type" :severity="locationTypeSeverity[location.type]" /></dd>

      <dt class="text-surface-500">Temperature Zone</dt>
      <dd>
        <StatusBadge
          :value="location.temperatureZone"
          :severity="temperatureZoneSeverity[location.temperatureZone]"
        />
      </dd>

      <dt class="text-surface-500">Units</dt>
      <dd class="text-surface-900">
        <template v-if="location.capacity != null">
          {{ location.occupancy }} / {{ location.capacity }} units
          <span class="text-surface-500"
            >({{ utilisationPercent(location.occupancy, location.capacity) }}%)</span
          >
        </template>
        <template v-else>
          {{ location.occupancy }} units · <span class="text-surface-500">Unlimited</span>
        </template>
      </dd>

      <dt class="text-surface-500">Weight</dt>
      <dd class="text-surface-900">
        <template v-if="location.weightCapacity != null">
          {{ location.weightOccupancy }} / {{ location.weightCapacity }} kg
          <span class="text-surface-500"
            >({{ utilisationPercent(location.weightOccupancy, location.weightCapacity) }}%)</span
          >
        </template>
        <template v-else>
          {{ location.weightOccupancy }} kg · <span class="text-surface-500">Unlimited</span>
        </template>
      </dd>

      <dt class="text-surface-500">Volume</dt>
      <dd class="text-surface-900">
        <template v-if="location.volumeCapacity != null">
          {{ location.volumeOccupancy }} / {{ location.volumeCapacity }} dm³
          <span class="text-surface-500"
            >({{ utilisationPercent(location.volumeOccupancy, location.volumeCapacity) }}%)</span
          >
        </template>
        <template v-else>
          {{ location.volumeOccupancy }} dm³ · <span class="text-surface-500">Unlimited</span>
        </template>
      </dd>

      <dt class="text-surface-500">Mixed SKUs</dt>
      <dd class="text-surface-900">{{ location.isMixedSkuAllowed ? 'Allowed' : 'Not allowed' }}</dd>

      <dt class="text-surface-500">Mixed Lots</dt>
      <dd class="text-surface-900">{{ location.isMixedLotAllowed ? 'Allowed' : 'Not allowed' }}</dd>

      <dt class="text-surface-500">Status</dt>
      <dd>
        <StatusBadge v-if="location.isBlocked" value="Blocked" severity="danger" />
        <StatusBadge v-else-if="location.isActive" value="Active" severity="success" />
        <StatusBadge v-else value="Inactive" severity="secondary" />
      </dd>

      <template v-if="location.isBlocked && location.blockedReason">
        <dt class="text-surface-500">Blocked Reason</dt>
        <dd class="text-surface-900">{{ location.blockedReason }}</dd>
      </template>

      <dt class="text-surface-500">Description</dt>
      <dd class="text-surface-900">{{ location.description || '—' }}</dd>
    </dl>
  </section>
</template>
