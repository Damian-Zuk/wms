<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import PageHeader from '@/components/common/PageHeader.vue'
import ProductForm from './ProductForm.vue'
import { useCreateProduct } from './useProducts'
import type { ProductFormValues } from '@/types/products'

const router = useRouter()
const toast = useToast()
const create = useCreateProduct()

const submitting = computed(() => create.isPending.value)
const serverErrors = ref<Record<string, string[]>>({})

const initialValues: ProductFormValues = {
  sku: '',
  name: '',
  description: '',
  requiredTemperatureZone: 'Ambient',
  preferredLocationIds: [],
}

function onSubmit(values: ProductFormValues) {
  serverErrors.value = {}
  create.mutate(values, {
    onSuccess: (newId) => {
      toast.add({ severity: 'success', summary: 'Product created', life: 3000 })
      router.push({ name: 'product-detail', params: { id: newId } })
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
    <PageHeader title="New Product" />
    <ProductForm
      mode="create"
      :initial-values="initialValues"
      :submitting="submitting"
      :server-errors="serverErrors"
      @submit="onSubmit"
      @cancel="router.push({ name: 'products' })"
    />
  </section>
</template>
