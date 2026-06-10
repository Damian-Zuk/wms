<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import PageHeader from '@/components/common/PageHeader.vue'
import RefreshButton from '@/components/common/RefreshButton.vue'
import AdjustInventoryDialog from './AdjustInventoryDialog.vue'
import TransferStockDialog from './TransferStockDialog.vue'
import { useInventory } from './useInventory'
import { useAuthStore } from '@/stores/auth'
import { formatCurrency } from '@/lib/money'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const id = computed(() => route.params.id as string)
const { data: inventory, isLoading, isFetching, isError, error, refetch } = useInventory(id)

const adjustVisible = ref(false)
const transferVisible = ref(false)
</script>

<template>
  <section class="p-6 flex flex-col gap-6" style="max-width: 900px">
    <PageHeader
      :title="inventory ? inventory.product.sku : 'Inventory'"
      :subtitle="inventory?.product.name"
    >
      <template #title-actions>
        <RefreshButton :loading="isFetching" @click="() => refetch()" />
      </template>
      <template #actions>
        <Button
          label="Back"
          icon="pi pi-arrow-left"
          severity="secondary"
          text
          @click="router.push({ name: 'inventory' })"
        />
        <Button
          v-if="auth.canMutate && inventory"
          label="Adjust"
          icon="pi pi-sliders-h"
          @click="adjustVisible = true"
        />

        <Button
          v-if="auth.canMutate && inventory"
          label="Transfer"
          icon="pi pi-arrow-right-arrow-left"
          @click="transferVisible = true"
        />
      </template>
    </PageHeader>

    <div v-if="isLoading" class="flex justify-center py-10">
      <ProgressSpinner />
    </div>

    <Message v-else-if="isError" severity="error" :closable="false">
      {{ error?.message ?? 'Failed to load inventory.' }}
    </Message>

    <template v-else-if="inventory">
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="rounded-xl border border-surface-200 bg-white p-5 text-center">
          <div class="text-sm text-surface-500">On Hand</div>
          <div class="text-3xl font-semibold text-surface-900">{{ inventory.onHand }}</div>
        </div>
        <div class="rounded-xl border border-surface-200 bg-white p-5 text-center">
          <div class="text-sm text-surface-500">Reserved</div>
          <div class="text-3xl font-semibold text-surface-900">{{ inventory.reserved }}</div>
        </div>
        <div class="rounded-xl border border-surface-200 bg-white p-5 text-center">
          <div class="text-sm text-surface-500">Available</div>
          <div class="text-3xl font-semibold text-primary-600">{{ inventory.available }}</div>
        </div>
        <div class="rounded-xl border border-surface-200 bg-white p-5 text-center">
          <div class="text-sm text-surface-500">On-hand Value</div>
          <div class="text-3xl font-semibold text-surface-900">{{ formatCurrency(inventory.onHandValue) }}</div>
        </div>
      </div>

      <dl
        class="grid grid-cols-[10rem_1fr] gap-y-4 gap-x-4 rounded-xl border border-surface-200 bg-white p-6"
      >
        <dt class="text-surface-500">Product</dt>
        <dd>
          <RouterLink
            :to="{ name: 'product-detail', params: { id: inventory.product.id } }"
            class="text-primary-600 hover:underline"
          >
            {{ inventory.product.sku }} — {{ inventory.product.name }}
          </RouterLink>
        </dd>

        <dt class="text-surface-500">Location</dt>
        <dd>
          <RouterLink
            :to="{ name: 'location-detail', params: { id: inventory.location.id } }"
            class="text-primary-600 hover:underline"
          >
            {{ inventory.location.code }} · {{ inventory.location.address }}
          </RouterLink>
        </dd>

        <dt class="text-surface-500">Lot</dt>
        <dd>
          <RouterLink
            v-if="inventory.lot"
            :to="{ name: 'lot-detail', params: { id: inventory.lot.id } }"
            class="text-primary-600 hover:underline"
          >
            {{ inventory.lot.number }}
          </RouterLink>
          <span v-else class="text-surface-900">—</span>
        </dd>
      </dl>
    </template>

    <AdjustInventoryDialog v-model:visible="adjustVisible" :inventory="inventory ?? null" />
    <TransferStockDialog v-model:visible="transferVisible" :inventory="inventory ?? null" />
  </section>
</template>
