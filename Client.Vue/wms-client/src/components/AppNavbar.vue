<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import Button from 'primevue/button'
import Menu from 'primevue/menu'

const auth = useAuthStore()
const router = useRouter()
const menu = ref<InstanceType<typeof Menu> | null>(null)

const displayName = computed(() => {
  if (!auth.user) return ''
  const full = `${auth.user.firstName ?? ''} ${auth.user.lastName ?? ''}`.trim()
  return full || auth.user.userName
})

const menuItems = computed(() => [
  ...(auth.hasRole('Admin')
    ? [
        {
          label: 'Admin panel',
          icon: 'pi pi-shield',
          command: () => router.push({ name: 'admin' }),
        },
        { separator: true },
      ]
    : []),
  {
    label: 'Logout',
    icon: 'pi pi-sign-out',
    command: () => onLogout(),
  },
])

function toggleMenu(event: Event) {
  menu.value?.toggle(event)
}

function onLogout() {
  auth.logout()
  router.push({ name: 'login' })
}
</script>

<template>
  <header
    class="flex items-center justify-between h-14 px-6 border-b border-surface-200 bg-white"
  >
    <RouterLink to="/" class="text-xl font-bold tracking-tight text-surface-900">
      WMS
    </RouterLink>

    <div>
      <template v-if="auth.isAuthenticated">
        <Button
          text
          severity="secondary"
          icon="pi pi-user"
          :label="displayName"
          aria-haspopup="true"
          aria-controls="user-menu"
          @click="toggleMenu"
        />
        <Menu id="user-menu" ref="menu" :model="menuItems" popup />
      </template>

      <Button
        v-else
        label="Login"
        icon="pi pi-sign-in"
        @click="router.push({ name: 'login' })"
      />
    </div>
  </header>
</template>
