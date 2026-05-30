<script setup lang="ts">
interface NavItem {
  label: string
  icon: string
  to: string
}

const items: NavItem[] = [
  { label: 'Dashboard', icon: 'pi pi-home', to: '/' },
  { label: 'Product Catalog', icon: 'pi pi-box', to: '/products' },
  { label: 'Locations', icon: 'pi pi-map-marker', to: '/locations' },
  { label: 'Lots', icon: 'pi pi-tags', to: '/lots' },
]
</script>

<template>
  <aside
    class="w-16 shrink-0 border-r border-surface-200 bg-white flex flex-col items-center py-3 gap-1"
  >
    <RouterLink
      v-for="item in items"
      :key="item.to"
      :to="item.to"
      :title="item.label"
      :aria-label="item.label"
      class="w-11 h-11 flex items-center justify-center rounded-lg text-surface-500 transition-colors hover:bg-surface-100 hover:text-surface-900"
      active-class="!bg-primary-50 !text-primary-600"
    >
      <i :class="item.icon" class="text-lg" />
    </RouterLink>
  </aside>
</template>
