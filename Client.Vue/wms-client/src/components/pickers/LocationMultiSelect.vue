<script setup lang="ts">
import { computed } from 'vue'
import MultiSelect from 'primevue/multiselect'
import { useQuery } from '@tanstack/vue-query'
import { locationsApi } from '@/api/endpoints/locations'
import { qk } from '@/api/query-keys'

const model = defineModel<string[]>({ default: () => [] })

const { data, isLoading } = useQuery({
  queryKey: qk.locations.options(),
  queryFn: () => locationsApi.list({ page: 1, pageSize: 200 }),
  staleTime: 10 * 60_000,
})

const options = computed(() =>
  (data.value?.items ?? []).map((l) => ({
    label: `${l.code} — ${l.display}`,
    value: l.id,
  })),
)
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
