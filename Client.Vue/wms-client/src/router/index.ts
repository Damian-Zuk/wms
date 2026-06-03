import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('@/features/auth/LoginView.vue'),
      meta: { public: true },
    },
    {
      path: '/',
      name: 'dashboard',
      component: () => import('@/features/dashboard/Dashboard.vue'),
    },
    {
      path: '/products',
      name: 'products',
      component: () => import('@/features/products/ProductsView.vue'),
    },
    {
      path: '/products/new',
      name: 'product-create',
      component: () => import('@/features/products/ProductCreateView.vue'),
      meta: { requiresMutate: true },
    },
    {
      path: '/products/:id',
      name: 'product-detail',
      component: () => import('@/features/products/ProductDetailView.vue'),
    },
    {
      path: '/products/:id/edit',
      name: 'product-edit',
      component: () => import('@/features/products/ProductEditView.vue'),
      meta: { requiresMutate: true },
    },
    {
      path: '/locations',
      name: 'locations',
      component: () => import('@/features/locations/LocationsView.vue'),
    },
    {
      path: '/locations/new',
      name: 'location-create',
      component: () => import('@/features/locations/LocationCreateView.vue'),
      meta: { requiresMutate: true },
    },
    {
      path: '/locations/:id',
      name: 'location-detail',
      component: () => import('@/features/locations/LocationDetailView.vue'),
    },
    {
      path: '/locations/:id/edit',
      name: 'location-edit',
      component: () => import('@/features/locations/LocationEditView.vue'),
      meta: { requiresMutate: true },
    },
    {
      path: '/lots',
      name: 'lots',
      component: () => import('@/features/lots/LotsView.vue'),
    },
    {
      path: '/lots/new',
      name: 'lot-create',
      component: () => import('@/features/lots/LotCreateView.vue'),
      meta: { requiresMutate: true },
    },
    {
      path: '/lots/:id',
      name: 'lot-detail',
      component: () => import('@/features/lots/LotDetailView.vue'),
    },
    {
      path: '/lots/:id/edit',
      name: 'lot-edit',
      component: () => import('@/features/lots/LotEditView.vue'),
      meta: { requiresMutate: true },
    },
    {
      path: '/inventory',
      name: 'inventory',
      component: () => import('@/features/inventory/InventoryView.vue'),
    },
    {
      path: '/inventory/availability',
      name: 'inventory-availability',
      component: () => import('@/features/inventory/AvailabilityView.vue'),
    },
    {
      path: '/inventory/expiring',
      name: 'inventory-expiring',
      component: () => import('@/features/inventory/ExpiringView.vue'),
    },
    {
      path: '/inventory/:id',
      name: 'inventory-detail',
      component: () => import('@/features/inventory/InventoryDetailView.vue'),
    },
    {
      path: '/stock-ins',
      name: 'stock-ins',
      component: () => import('@/features/stock-ins/StockInsView.vue'),
    },
    {
      path: '/admin',
      name: 'admin',
      component: () => import('@/features/admin/AdminPanelView.vue'),
      meta: { requiresAdmin: true },
    },
    {
      path: '/stock-ins/new',
      name: 'stock-in-create',
      component: () => import('@/features/stock-ins/StockInCreateView.vue'),
      meta: { requiresMutate: true },
    },
    {
      path: '/stock-ins/:id',
      name: 'stock-in-detail',
      component: () => import('@/features/stock-ins/StockInDetailView.vue'),
    },
  ],
})

router.beforeEach((to) => {
  const auth = useAuthStore()

  if (!to.meta.public && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  if (to.name === 'login' && auth.isAuthenticated) {
    return { name: 'dashboard' }
  }

  // Routes that perform mutations require Admin/Manager.
  if (to.meta.requiresMutate && !auth.canMutate) {
    return { name: 'products' }
  }

  // Admin-only routes (e.g. the admin panel).
  if (to.meta.requiresAdmin && !auth.hasRole('Admin')) {
    return { name: 'dashboard' }
  }

  return true
})

export default router
