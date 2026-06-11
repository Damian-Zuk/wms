<script setup lang="ts">
import RefreshButton from './RefreshButton.vue'

defineProps<{
  title: string
  count: number
  loading?: boolean
}>()

const emit = defineEmits<{ refresh: [] }>()
</script>

<template>
  <div class="flex items-center gap-3">
    <h1 class="text-2xl font-semibold text-surface-900">{{ title }}</h1>
    <span class="rounded-full bg-surface-100 px-2 py-0.5 text-md font-medium text-surface-500 tabular-nums"
      style="transform: translateY(0.1em)">{{ count }}</span>
    <RefreshButton :loading="loading" @click="emit('refresh')" style="transform: translateY(0.05em)"/>
  </div>
</template>
