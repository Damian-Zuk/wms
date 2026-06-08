<script setup lang="ts">
import Select from 'primevue/select'
import { pickingStrategyLabel } from '@/lib/enum-display'
import type { PickingStrategyType } from '@/types/enums'

const model = defineModel<PickingStrategyType>({ default: 'Fefo' })

withDefaults(defineProps<{ disabled?: boolean }>(), { disabled: false })

// 'Manual' is not a plannable strategy — it's stamped only when a user hand-edits
// pick locations, so it must never appear as a choice when creating a stock-out.
const options = (Object.keys(pickingStrategyLabel) as PickingStrategyType[])
  .filter((value) => value !== 'Manual')
  .map((value) => ({
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
