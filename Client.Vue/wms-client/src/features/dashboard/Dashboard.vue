<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <section class="p-6">
    <h1 class="text-2xl font-semibold text-surface-900">Dashboard</h1>
    <p class="text-surface-500 mt-2">
      Welcome<span v-if="auth.user">, {{ auth.user.firstName }}</span
      >.
    </p>
  </section>
</template>
