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
import { useLocationOptions } from '@/features/locations/useLocations'
import { temperatureZoneSeverity } from '@/lib/enum-display'
import { useDeleteProduct, useProduct } from './useProducts'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const confirm = useConfirm()
const toast = useToast()

const id = computed(() => route.params.id as string)
const { data: product, isLoading, isFetching, isError, error, refetch } = useProduct(id)
const del = useDeleteProduct()
const { byId: locationsById } = useLocationOptions()

/** Preferred locations resolved to their code/address for display as links. */
const preferredLocations = computed(() =>
  (product.value?.preferredLocationIds ?? [])
    .map((locationId) => locationsById.value.get(locationId))
    .filter((l): l is NonNullable<typeof l> => l != null),
)

function checkInventory() {
  router.push({ name: 'inventory', query: { productId: id.value } })
}

function onEdit() {
  router.push({ name: 'product-edit', params: { id: id.value } })
}

function onDelete() {
  confirm.require({
    message: 'Delete this product? This cannot be undone.',
    header: 'Confirm delete',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', text: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: () => {
      del.mutate(id.value, {
        onSuccess: () => {
          toast.add({ severity: 'success', summary: 'Product deleted', life: 3000 })
          router.push({ name: 'products' })
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
    <PageHeader :title="product?.name ?? 'Product'" :subtitle="product?.sku">
      <template #title-actions>
        <RefreshButton :loading="isFetching" @click="() => refetch()" />
      </template>
      <template #actions>
        <Button
          label="Back"
          icon="pi pi-arrow-left"
          severity="secondary"
          text
          @click="router.push({ name: 'products' })"
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
      {{ error?.message ?? 'Failed to load product.' }}
    </Message>

    <dl
      v-else-if="product"
      class="grid grid-cols-[10rem_1fr] gap-y-4 gap-x-4 rounded-xl border border-surface-200 bg-white p-6"
    >
      <dt class="text-surface-500">SKU</dt>
      <dd class="text-surface-900 font-medium">{{ product.sku }}</dd>

      <dt class="text-surface-500">Name</dt>
      <dd class="text-surface-900">{{ product.name }}</dd>

      <dt class="text-surface-500">Description</dt>
      <dd class="text-surface-900">{{ product.description || '—' }}</dd>

      <dt class="text-surface-500">Weight</dt>
      <dd class="text-surface-900">{{ product.weight }} kg</dd>

      <dt class="text-surface-500">Volume</dt>
      <dd class="text-surface-900">{{ product.volume }} dm³</dd>

      <dt class="text-surface-500">Temperature Zone</dt>
      <dd>
        <StatusBadge
          :value="product.requiredTemperatureZone"
          :severity="temperatureZoneSeverity[product.requiredTemperatureZone]"
        />
      </dd>

      <dt class="text-surface-500">Preferred Locations</dt>
      <dd class="text-surface-900">
        <ul v-if="preferredLocations.length" class="flex flex-col gap-1">
          <li v-for="loc in preferredLocations" :key="loc.id">
            <RouterLink
              :to="{ name: 'location-detail', params: { id: loc.id } }"
              class="text-primary-600 hover:underline"
            >
              {{ loc.code }}
            </RouterLink>
            <span class="text-surface-500"> — {{ loc.display }}</span>
          </li>
        </ul>
        <span v-else class="text-surface-500">None configured</span>
      </dd>
    </dl>

    <template v-if="product">
      <section class="flex flex-col gap-3">
        <h2 class="text-lg font-semibold text-surface-900">Availability</h2>
        <AvailabilityPanel :product-id="product.id" />
      </section>

      <Button label="Check inventory" icon="pi pi-database" outlined fluid @click="checkInventory" />
    </template>
  </section>
</template>
