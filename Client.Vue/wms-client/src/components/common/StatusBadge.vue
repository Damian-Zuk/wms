<script setup lang="ts">
import Tag from 'primevue/tag'
import type { TagSeverity } from '@/lib/enum-display'

withDefaults(
  defineProps<{
    value: string
    severity?: TagSeverity
  }>(),
  { severity: 'secondary' },
)
</script>

<template>
  <Tag :value="value" :severity="severity" />
</template>
