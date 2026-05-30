<script setup lang="ts">
import Select from 'primevue/select'
import { useProductOptions } from '@/features/products/useProducts'

const model = defineModel<string | null>({ default: null })

withDefaults(
  defineProps<{
    disabled?: boolean
    showClear?: boolean
    placeholder?: string
  }>(),
  { disabled: false, showClear: false, placeholder: 'Select product' },
)

const { options, isLoading } = useProductOptions()
</script>

<template>
  <Select
    v-model="model"
    :options="options"
    option-label="label"
    option-value="value"
    :loading="isLoading"
    :disabled="disabled"
    :show-clear="showClear"
    :placeholder="placeholder"
    filter
    fluid
  />
</template>
