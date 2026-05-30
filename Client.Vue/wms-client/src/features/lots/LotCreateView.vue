<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import PageHeader from '@/components/common/PageHeader.vue'
import LotForm from './LotForm.vue'
import { useCreateLot } from './useLots'
import { toDateOnly } from '@/lib/date'
import type { LotFormValues } from '@/types/lots'

const router = useRouter()
const toast = useToast()
const create = useCreateLot()

const submitting = computed(() => create.isPending.value)
const serverErrors = ref<Record<string, string[]>>({})

const initialValues: LotFormValues = {
  number: '',
  productId: '',
  manufactureDate: null,
  expirationDate: null,
}

function onSubmit(values: LotFormValues) {
  serverErrors.value = {}
  create.mutate(
    {
      number: values.number,
      productId: values.productId,
      manufactureDate: toDateOnly(values.manufactureDate),
      expirationDate: toDateOnly(values.expirationDate),
    },
    {
      onSuccess: (newId) => {
        toast.add({ severity: 'success', summary: 'Lot created', life: 3000 })
        router.push({ name: 'lot-detail', params: { id: newId } })
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
    },
  )
}
</script>

<template>
  <section class="p-6 flex flex-col gap-6">
    <PageHeader title="New Lot" />
    <LotForm
      mode="create"
      :initial-values="initialValues"
      :submitting="submitting"
      :server-errors="serverErrors"
      @submit="onSubmit"
      @cancel="router.push({ name: 'lots' })"
    />
  </section>
</template>
