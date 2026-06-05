<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import PageHeader from '@/components/common/PageHeader.vue'
import LocationForm from './LocationForm.vue'
import { useCreateLocation } from './useLocations'
import { toCommand } from './location-mapper'
import type { LocationFormValues } from '@/types/locations'

const router = useRouter()
const toast = useToast()
const create = useCreateLocation()

const submitting = computed(() => create.isPending.value)
const serverErrors = ref<Record<string, string[]>>({})

const initialValues: LocationFormValues = {
  code: '',
  zone: '',
  aisle: '',
  rack: '',
  shelf: '',
  bin: '',
  type: 'Storage',
  temperatureZone: 'Ambient',
  capacity: null,
  weightCapacity: null,
  volumeCapacity: null,
  description: '',
  isMixedSkuAllowed: true,
  isMixedLotAllowed: true,
}

function onSubmit(values: LocationFormValues) {
  serverErrors.value = {}
  create.mutate(toCommand(values), {
    onSuccess: (newId) => {
      toast.add({ severity: 'success', summary: 'Location created', life: 3000 })
      router.push({ name: 'location-detail', params: { id: newId } })
    },
    onError: (err) => {
      serverErrors.value = err.fieldErrors
      if (Object.keys(err.fieldErrors).length === 0) {
        toast.add({
          severity: 'error',
          summary: 'Create failed',
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
    <PageHeader title="New Location" />
    <LocationForm
      mode="create"
      :initial-values="initialValues"
      :submitting="submitting"
      :server-errors="serverErrors"
      @submit="onSubmit"
      @cancel="router.push({ name: 'locations' })"
    />
  </section>
</template>
