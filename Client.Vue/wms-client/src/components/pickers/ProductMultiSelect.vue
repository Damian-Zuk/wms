<script setup lang="ts">
import MultiSelect from 'primevue/multiselect'
import { useProductOptions } from '@/features/products/useProducts'

const model = defineModel<string[]>({ default: () => [] })

const { options, isLoading } = useProductOptions()
</script>

<template>
  <MultiSelect
    v-model="model"
    :options="options"
    option-label="label"
    option-value="value"
    :loading="isLoading"
    filter
    display="chip"
    placeholder="Select preferred products"
    fluid
  />
</template>
