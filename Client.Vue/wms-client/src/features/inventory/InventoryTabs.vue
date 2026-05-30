<script setup lang="ts">
import type { RouteLocationRaw } from 'vue-router'

const tabs: { label: string; to: RouteLocationRaw }[] = [
  { label: 'Stock', to: { name: 'inventory' } },
  { label: 'Availability', to: { name: 'inventory-availability' } },
  { label: 'Expiring', to: { name: 'inventory-expiring' } },
]
</script>

<template>
  <nav class="flex gap-1 border-b border-surface-200">
    <RouterLink
      v-for="tab in tabs"
      :key="tab.label"
      :to="tab.to"
      class="px-4 py-2 text-sm font-medium text-surface-500 border-b-2 border-transparent hover:text-surface-900"
      exact-active-class="!text-primary-600 !border-primary-500"
    >
      {{ tab.label }}
    </RouterLink>
  </nav>
</template>
