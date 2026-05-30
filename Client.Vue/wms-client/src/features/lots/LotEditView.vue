<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import ProgressSpinner from 'primevue/progressspinner'
import PageHeader from '@/components/common/PageHeader.vue'
import LotForm from './LotForm.vue'
import { useLot, useUpdateLot } from './useLots'
import { fromDateOnly, toDateOnly } from '@/lib/date'
import type { LotFormValues } from '@/types/lots'

const route = useRoute()
const router = useRouter()
const toast = useToast()

const id = computed(() => route.params.id as string)
const { data: lot, isLoading } = useLot(id)
const update = useUpdateLot(id.value)

const submitting = computed(() => update.isPending.value)
const serverErrors = ref<Record<string, string[]>>({})
const initialValues = ref<LotFormValues | null>(null)

watch(
  lot,
  (l) => {
    if (l) {
      initialValues.value = {
        number: l.number,
        productId: l.productId,
        manufactureDate: fromDateOnly(l.manufactureDate),
        expirationDate: fromDateOnly(l.expirationDate),
      }
    }
  },
  { immediate: true },
)

function onSubmit(values: LotFormValues) {
  serverErrors.value = {}
  update.mutate(
    {
      manufactureDate: toDateOnly(values.manufactureDate),
      expirationDate: toDateOnly(values.expirationDate),
    },
    {
      onSuccess: () => {
        toast.add({ severity: 'success', summary: 'Lot updated', life: 3000 })
        router.push({ name: 'lot-detail', params: { id: id.value } })
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
    },
  )
}
</script>

<template>
  <section class="p-6 flex flex-col gap-6">
    <PageHeader title="Edit Lot" :subtitle="lot?.number" />

    <div v-if="isLoading && !initialValues" class="flex justify-center py-10">
      <ProgressSpinner />
    </div>

    <LotForm
      v-else-if="initialValues"
      mode="edit"
      :initial-values="initialValues"
      :submitting="submitting"
      :server-errors="serverErrors"
      @submit="onSubmit"
      @cancel="router.push({ name: 'lot-detail', params: { id } })"
    />
  </section>
</template>
