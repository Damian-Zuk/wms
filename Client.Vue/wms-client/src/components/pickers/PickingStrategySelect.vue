<script setup lang="ts">
import Select from 'primevue/select'
import { pickingStrategyLabel } from '@/lib/enum-display'
import type { PickingStrategyType } from '@/types/enums'

const model = defineModel<PickingStrategyType>({ default: 'Fefo' })

withDefaults(defineProps<{ disabled?: boolean }>(), { disabled: false })

const options = (Object.keys(pickingStrategyLabel) as PickingStrategyType[]).map((value) => ({
  value,
  label: pickingStrategyLabel[value],
}))
</script>

<template>
  <Select
    v-model="model"
    :options="options"
    option-label="label"
    option-value="value"
    :disabled="disabled"
    fluid
  />
</template>
