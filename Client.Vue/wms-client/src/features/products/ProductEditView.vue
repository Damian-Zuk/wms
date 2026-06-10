<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import ProgressSpinner from 'primevue/progressspinner'
import PageHeader from '@/components/common/PageHeader.vue'
import ProductForm from './ProductForm.vue'
import { useProduct, useUpdateProduct } from './useProducts'
import type { ProductFormValues, UpdateProductRequest } from '@/types/products'

const route = useRoute()
const router = useRouter()
const toast = useToast()

const id = computed(() => route.params.id as string)
const { data: product, isLoading } = useProduct(id)
const update = useUpdateProduct(id.value)

const submitting = computed(() => update.isPending.value)
const serverErrors = ref<Record<string, string[]>>({})
const initialValues = ref<ProductFormValues | null>(null)

watch(
  product,
  (p) => {
    if (p) {
      initialValues.value = {
        sku: p.sku,
        name: p.name,
        description: p.description,
        weight: p.weight,
        volume: p.volume,
        unitPrice: p.unitPrice,
        requiredTemperatureZone: p.requiredTemperatureZone,
        preferredLocationIds: [...p.preferredLocationIds],
        categoryId: p.categoryId,
      }
    }
  },
  { immediate: true },
)

function onSubmit(values: ProductFormValues) {
  serverErrors.value = {}
  const body: UpdateProductRequest = {
    name: values.name,
    description: values.description,
    weight: values.weight,
    volume: values.volume,
    unitPrice: values.unitPrice,
    requiredTemperatureZone: values.requiredTemperatureZone,
    preferredLocationIds: values.preferredLocationIds,
    categoryId: values.categoryId,
  }
  update.mutate(body, {
    onSuccess: () => {
      toast.add({ severity: 'success', summary: 'Product updated', life: 3000 })
      router.push({ name: 'product-detail', params: { id: id.value } })
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
    <PageHeader title="Edit Product" :subtitle="product?.sku" />

    <div v-if="isLoading && !initialValues" class="flex justify-center py-10">
      <ProgressSpinner />
    </div>

    <ProductForm
      v-else-if="initialValues"
      mode="edit"
      :initial-values="initialValues"
      :submitting="submitting"
      :server-errors="serverErrors"
      @submit="onSubmit"
      @cancel="router.push({ name: 'product-detail', params: { id } })"
    />
  </section>
</template>
