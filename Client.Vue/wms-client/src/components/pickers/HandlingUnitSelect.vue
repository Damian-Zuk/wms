<script setup lang="ts">
import { toRef } from 'vue'
import Select from 'primevue/select'
import { useHandlingUnitOptions } from '@/features/handling-units/useHandlingUnits'

const model = defineModel<string | null>({ default: null })

const props = withDefaults(
  defineProps<{
    locationId?: string
    disabled?: boolean
    showClear?: boolean
    placeholder?: string
  }>(),
  { disabled: false, showClear: true, placeholder: 'No handling unit' },
)

const { options, isLoading } = useHandlingUnitOptions(toRef(props, 'locationId'))
</script>

<template>
  <Select
    v-model="model"
    :options="options"
    option-label="label"
    option-value="value"
    :loading="isLoading"
    :disabled="disabled || !locationId"
    :show-clear="showClear"
    :placeholder="locationId ? placeholder : 'Select location first'"
    filter
    fluid
  />
</template>
