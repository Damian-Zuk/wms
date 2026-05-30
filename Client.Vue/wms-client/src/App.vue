<script setup lang="ts">
import Toast from 'primevue/toast'
import ConfirmDialog from 'primevue/confirmdialog'
import AppNavbar from '@/components/AppNavbar.vue'
import AppSidebar from '@/components/AppSidebar.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <div class="min-h-svh flex flex-col bg-surface-50">
    <AppNavbar />
    <div class="flex flex-1 min-h-0">
      <AppSidebar v-if="auth.isAuthenticated" />
      <main class="flex-1 overflow-auto">
        <RouterView />
      </main>
    </div>
  </div>

  <Toast position="top-center" />
  <ConfirmDialog />
</template>
