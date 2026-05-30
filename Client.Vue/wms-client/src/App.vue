<script setup lang="ts">
import AppNavbar from '@/components/AppNavbar.vue'
</script>

<template>
  <div class="min-h-svh flex flex-col bg-surface-50">
    <AppNavbar />
    <main class="flex-1">
      <RouterView />
    </main>
  </div>
</template>
