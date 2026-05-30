<script setup lang="ts">
defineProps<{
  title: string
  subtitle?: string
}>()
</script>

<template>
  <div class="flex items-start justify-between gap-4">
    <div>
      <h1 class="text-2xl font-semibold text-surface-900">{{ title }}</h1>
      <p v-if="subtitle" class="text-surface-500 mt-1">{{ subtitle }}</p>
    </div>
    <div class="flex items-center gap-2 shrink-0">
      <slot name="actions" />
    </div>
  </div>
</template>
