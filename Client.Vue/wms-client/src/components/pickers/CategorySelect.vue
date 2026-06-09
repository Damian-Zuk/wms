<script setup lang="ts">
import Select from 'primevue/select'
import { useCategoryOptions } from '@/features/categories/useCategories'

const model = defineModel<string | null>({ default: null })

withDefaults(
  defineProps<{
    disabled?: boolean
    showClear?: boolean
    placeholder?: string
  }>(),
  { disabled: false, showClear: false, placeholder: 'Select category' },
)

const { options, isLoading } = useCategoryOptions()
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
