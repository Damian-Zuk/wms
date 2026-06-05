<script setup lang="ts">
import Button from 'primevue/button'

defineProps<{
  loading?: boolean
}>()

const emit = defineEmits<{ click: [] }>()
</script>

<template>
  <Button
    icon="pi pi-refresh"
    severity="secondary"
    style="transform: translateY(0.05em)"
    outlined
    rounded
    :loading="loading"
    aria-label="Refresh"
    title="Refresh"
    @click="emit('click')"
  />
</template>
