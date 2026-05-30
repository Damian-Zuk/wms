<script setup lang="ts">
import MultiSelect from 'primevue/multiselect'
import { useLocationOptions } from '@/features/locations/useLocations'

const model = defineModel<string[]>({ default: () => [] })

const { options, isLoading } = useLocationOptions()
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
    placeholder="Select preferred locations"
    fluid
  />
</template>
