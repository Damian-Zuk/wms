<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import ProgressSpinner from 'primevue/progressspinner'
import PageHeader from '@/components/common/PageHeader.vue'
import LocationForm from './LocationForm.vue'
import { useLocation, useUpdateLocation } from './useLocations'
import { toCommand } from './location-mapper'
import type { LocationFormValues } from '@/types/locations'

const route = useRoute()
const router = useRouter()
const toast = useToast()

const id = computed(() => route.params.id as string)
const { data: location, isLoading } = useLocation(id)
const update = useUpdateLocation(id.value)

const submitting = computed(() => update.isPending.value)
const serverErrors = ref<Record<string, string[]>>({})
const initialValues = ref<LocationFormValues | null>(null)

watch(
  location,
  (l) => {
    if (l) {
      initialValues.value = {
        code: l.code,
        zone: l.address.zone,
        aisle: l.address.aisle,
        rack: l.address.rack,
        shelf: l.address.shelf,
        bin: l.address.bin,
        type: l.type,
        temperatureZone: l.temperatureZone,
        capacity: l.capacity,
        description: l.description ?? '',
        isMixedSkuAllowed: l.isMixedSkuAllowed,
        isMixedLotAllowed: l.isMixedLotAllowed,
      }
    }
  },
  { immediate: true },
)

function onSubmit(values: LocationFormValues) {
  serverErrors.value = {}
  update.mutate(toCommand(values), {
    onSuccess: () => {
      toast.add({ severity: 'success', summary: 'Location updated', life: 3000 })
      router.push({ name: 'location-detail', params: { id: id.value } })
    },
    onError: (err) => {
      serverErrors.value = err.fieldErrors
      if (Object.keys(err.fieldErrors).length === 0) {
        toast.add({
          severity: 'error',
          summary: 'Update failed',
          detail: err.message,
          life: 5000,
        })
      }
    },
  })
}
</script>

<template>
  <section class="p-6 flex flex-col gap-6">
    <PageHeader title="Edit Location" :subtitle="location?.code" />

    <div v-if="isLoading && !initialValues" class="flex justify-center py-10">
      <ProgressSpinner />
    </div>

    <LocationForm
      v-else-if="initialValues"
      mode="edit"
      :initial-values="initialValues"
      :submitting="submitting"
      :server-errors="serverErrors"
      @submit="onSubmit"
      @cancel="router.push({ name: 'location-detail', params: { id } })"
    />
  </section>
</template>
