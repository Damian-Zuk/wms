<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import PageHeader from '@/components/common/PageHeader.vue'
import RefreshButton from '@/components/common/RefreshButton.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import AvailabilityPanel from '@/features/inventory/AvailabilityPanel.vue'
import { useAuthStore } from '@/stores/auth'
import { useProductOptions } from '@/features/products/useProducts'
import { formatDate } from '@/lib/date'
import { lotStatus } from './lot-status'
import { useDeleteLot, useLot } from './useLots'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const confirm = useConfirm()
const toast = useToast()

const id = computed(() => route.params.id as string)
const { data: lot, isLoading, isFetching, isError, error, refetch } = useLot(id)
const del = useDeleteLot()
const { byId } = useProductOptions()

const productLabel = computed(() => {
  if (!lot.value) return ''
  const p = byId.value.get(lot.value.productId)
  return p ? `${p.sku} — ${p.name}` : lot.value.productId
})

function onEdit() {
  router.push({ name: 'lot-edit', params: { id: id.value } })
}

function checkInventory() {
  if (!lot.value) return
  router.push({
    name: 'inventory',
    query: { productId: lot.value.productId, lotId: lot.value.id },
  })
}

function onDelete() {
  confirm.require({
    message: 'Delete this lot? This cannot be undone.',
    header: 'Confirm delete',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', text: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: () => {
      del.mutate(id.value, {
        onSuccess: () => {
          toast.add({ severity: 'success', summary: 'Lot deleted', life: 3000 })
          router.push({ name: 'lots' })
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
    <PageHeader :title="lot ? `Lot ${lot.number}` : 'Lot'" :subtitle="productLabel">
      <template #title-actions>
        <RefreshButton :loading="isFetching" @click="() => refetch()" />
      </template>
      <template #actions>
        <Button
          label="Back"
          icon="pi pi-arrow-left"
          severity="secondary"
          text
          @click="router.push({ name: 'lots' })"
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
      {{ error?.message ?? 'Failed to load lot.' }}
    </Message>

    <dl
      v-else-if="lot"
      class="grid grid-cols-[12rem_1fr] gap-y-4 gap-x-4 rounded-xl border border-surface-200 bg-white p-6"
    >
      <dt class="text-surface-500">Lot Number</dt>
      <dd class="text-surface-900 font-medium">{{ lot.number }}</dd>

      <dt class="text-surface-500">Product</dt>
      <dd class="text-surface-900">{{ productLabel }}</dd>

      <dt class="text-surface-500">Manufacture Date</dt>
      <dd class="text-surface-900">{{ formatDate(lot.manufactureDate) }}</dd>

      <dt class="text-surface-500">Expiration Date</dt>
      <dd class="text-surface-900">{{ formatDate(lot.expirationDate) }}</dd>

      <dt class="text-surface-500">Status</dt>
      <dd><StatusBadge :value="lotStatus(lot).label" :severity="lotStatus(lot).severity" /></dd>
    </dl>

    <template v-if="lot">
      <section class="flex flex-col gap-3">
        <h2 class="text-lg font-semibold text-surface-900">Availability</h2>
        <AvailabilityPanel :product-id="lot.productId" :lot-id="lot.id" />
      </section>

      <Button label="Check inventory" icon="pi pi-database" outlined fluid @click="checkInventory" />
    </template>
  </section>
</template>
