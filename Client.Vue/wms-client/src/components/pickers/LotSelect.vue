<script setup lang="ts">
import { toRef } from 'vue'
import Select from 'primevue/select'
import { useLotOptions } from '@/features/lots/useLots'

const model = defineModel<string | null>({ default: null })

const props = withDefaults(
  defineProps<{
    productId?: string
    disabled?: boolean
    showClear?: boolean
    placeholder?: string
  }>(),
  { disabled: false, showClear: true, placeholder: 'No lot' },
)

const { options, isLoading } = useLotOptions(toRef(props, 'productId'))
</script>

<template>
  <Select
    v-model="model"
    :options="options"
    option-label="label"
    option-value="value"
    :loading="isLoading"
    :disabled="disabled || !productId"
    :show-clear="showClear"
    :placeholder="productId ? placeholder : 'Select product first'"
    filter
    fluid
  />
</template>
