<script setup lang="ts">
import { computed } from 'vue'
import VueApexCharts from 'vue3-apexcharts'
import type { ApexOptions } from 'apexcharts'
import RefreshButton from '@/components/common/RefreshButton.vue'
import StatCard from './components/StatCard.vue'
import { useInventoryOverview } from './useDashboard'
import { donutOptions, horizontalBarOptions } from './dashboard-helpers'
import { formatCurrency, formatCurrencyCompact } from '@/lib/money'

const { data, isLoading, isFetching, isError, refetch } = useInventoryOverview()

const summary = computed(() => data.value?.summary)
const byZone = computed(() => data.value?.byTemperatureZone ?? [])
const byType = computed(() => data.value?.byLocationType ?? [])
const top = computed(() => data.value?.topProducts ?? [])
const expiry = computed(() => data.value?.expiryBuckets)
const expiryValue = computed(() => data.value?.expiryValueBuckets)

const cards = computed(() => {
  const s = summary.value
  if (!s) return []
  return [
    { title: 'Inventory value', value: formatCurrencyCompact(s.totalValue), icon: 'pi pi-wallet', tone: 'blue' as const },
    { title: 'On-hand units', value: s.onHandUnits, icon: 'pi pi-database', tone: 'default' as const },
    { title: 'Available units', value: s.availableUnits, icon: 'pi pi-check-circle', tone: 'green' as const },
    { title: 'Reserved units', value: s.reservedUnits, icon: 'pi pi-lock', tone: 'amber' as const },
    { title: 'Distinct SKUs', value: s.distinctSkus, icon: 'pi pi-box', tone: 'default' as const },
  ]
})

// Composition doughnuts
const zoneColors: Record<string, string> = { Ambient: '#f59e0b', Chilled: '#06b6d4', Frozen: '#6366f1' }
const typeColors: Record<string, string> = { Storage: '#3b82f6', Quarantine: '#f59e0b', Returns: '#64748b' }

const zoneLabels = computed(() => byZone.value.map((s) => s.key))
const zoneSeries = computed(() => byZone.value.map((s) => s.units))
const zoneOpts = computed(() => donutOptions(zoneLabels.value, zoneLabels.value.map((k) => zoneColors[k] ?? '#94a3b8')))

const typeLabels = computed(() => byType.value.map((s) => s.key))
const typeSeries = computed(() => byType.value.map((s) => s.units))
const typeOpts = computed(() => donutOptions(typeLabels.value, typeLabels.value.map((k) => typeColors[k] ?? '#94a3b8')))

// Top SKUs
const topCategories = computed(() => top.value.map((p) => p.sku))
const topSeries = computed(() => [{ name: 'Units', data: top.value.map((p) => p.units) }])
const topOpts = computed(() => horizontalBarOptions(topCategories.value, '#6366f1'))

// Expiry buckets
const expiryCategories = ['Expired', '≤7d', '8–30d', '31–60d', '61–90d', '90d+']
const expiryColors = ['#ef4444', '#f97316', '#f59e0b', '#eab308', '#84cc16', '#22c55e']
const expirySeries = computed(() => {
  const e = expiry.value
  return [{ name: 'Units', data: e ? [e.expired, e.within7, e.within30, e.within60, e.within90, e.beyond90] : [] }]
})
const expiryHasData = computed(() => expirySeries.value[0].data.some((v) => v > 0))
const expiryOpts = computed<ApexOptions>(() => ({
  chart: { type: 'bar', toolbar: { show: false }, fontFamily: 'inherit' },
  colors: expiryColors,
  plotOptions: { bar: { distributed: true, borderRadius: 4, columnWidth: '55%' } },
  dataLabels: { enabled: false },
  legend: { show: false },
  xaxis: {
    categories: expiryCategories,
    axisBorder: { show: false },
    axisTicks: { show: false },
    labels: { style: { colors: '#94a3b8' } },
  },
  yaxis: { labels: { formatter: (v: number) => Math.round(v).toString(), style: { colors: '#94a3b8' } } },
  grid: { borderColor: '#e5e7eb', strokeDashArray: 4 },
  tooltip: { y: { formatter: (v: number) => `${v} units` } },
}))

const expiringSoon = computed(() => (expiry.value ? expiry.value.within7 + expiry.value.within30 : 0))

// Value at risk — on-hand stock value bucketed by time to expiry.
const expiredValue = computed(() => expiryValue.value?.expired ?? 0)
const expiringSoonValue = computed(() =>
  expiryValue.value ? expiryValue.value.within7 + expiryValue.value.within30 : 0,
)
const noExpiryValue = computed(() => expiryValue.value?.noExpiry ?? 0)
</script>

<template>
  <div class="flex flex-col gap-4">
    <div class="flex items-center justify-end">
      <RefreshButton :loading="isFetching" @click="() => refetch()" />
    </div>

    <div v-if="isError" class="rounded-lg border border-red-200 bg-red-50 text-red-700 p-4">
      Failed to load inventory data.
    </div>

    <div v-else-if="isLoading" class="grid grid-cols-2 lg:grid-cols-5 gap-4">
      <div v-for="n in 5" :key="n" class="h-28 rounded-xl border border-surface-200 bg-surface-100 animate-pulse" />
    </div>

    <template v-else>
      <!-- KPIs -->
      <div class="grid grid-cols-2 lg:grid-cols-5 gap-4">
        <StatCard
          v-for="card in cards"
          :key="card.title"
          :title="card.title"
          :value="card.value"
          :icon="card.icon"
          :tone="card.tone"
        />
      </div>
      <p class="text-xs text-surface-500 -mt-2">
        Total weight {{ Math.round(summary?.totalWeightKg ?? 0).toLocaleString() }} kg ·
        Total volume {{ Math.round(summary?.totalVolume ?? 0).toLocaleString() }} dm³
      </p>

      <!-- Composition -->
      <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">
        <div class="rounded-xl border border-surface-200 bg-white p-4 flex flex-col">
          <h2 class="text-base font-semibold text-surface-900 mb-2">Stock by temperature zone</h2>
          <div v-if="zoneSeries.length" class="flex-1 flex items-center justify-center" style="height: 280px">
            <VueApexCharts type="donut" height="280" :options="zoneOpts" :series="zoneSeries" class="w-full" />
          </div>
          <p v-else class="text-sm text-surface-500 py-8 text-center">No stock on hand.</p>
        </div>

        <div class="rounded-xl border border-surface-200 bg-white p-4 flex flex-col">
          <h2 class="text-base font-semibold text-surface-900 mb-2">Stock by location type</h2>
          <div v-if="typeSeries.length" class="flex-1 flex items-center justify-center" style="height: 280px">
            <VueApexCharts type="donut" height="280" :options="typeOpts" :series="typeSeries" class="w-full" />
          </div>
          <p v-else class="text-sm text-surface-500 py-8 text-center">No stock on hand.</p>
        </div>
      </div>

      <!-- Expiry -->
      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="xl:col-span-2 rounded-xl border border-surface-200 bg-white p-4">
          <h2 class="text-base font-semibold text-surface-900 mb-2">Stock by time to expiry</h2>
          <VueApexCharts v-if="expiryHasData" type="bar" height="280" :options="expiryOpts" :series="expirySeries" />
          <p v-else class="text-sm text-surface-500 py-8 text-center">No lot-tracked stock with expiry dates.</p>
        </div>

        <div class="rounded-xl border border-surface-200 bg-white p-4 flex flex-col gap-3">
          <h2 class="text-base font-semibold text-surface-900">Value at risk</h2>
          <div class="flex items-center justify-between rounded-lg border border-red-200 bg-red-50 text-red-700 p-3">
            <span class="flex flex-col">
              <span class="text-sm">Expired on hand</span>
              <span class="text-xs opacity-75">{{ (expiry?.expired ?? 0).toLocaleString() }} units</span>
            </span>
            <span class="text-xl font-semibold">{{ formatCurrency(expiredValue) }}</span>
          </div>
          <div class="flex items-center justify-between rounded-lg border border-amber-200 bg-amber-50 text-amber-700 p-3">
            <span class="flex flex-col">
              <span class="text-sm">Expiring ≤ 30 days</span>
              <span class="text-xs opacity-75">{{ expiringSoon.toLocaleString() }} units</span>
            </span>
            <span class="text-xl font-semibold">{{ formatCurrency(expiringSoonValue) }}</span>
          </div>
          <div class="flex items-center justify-between rounded-lg border border-surface-200 bg-surface-50 text-surface-600 p-3">
            <span class="flex flex-col">
              <span class="text-sm">No expiry date</span>
              <span class="text-xs opacity-75">{{ (expiry?.noExpiry ?? 0).toLocaleString() }} units</span>
            </span>
            <span class="text-xl font-semibold">{{ formatCurrency(noExpiryValue) }}</span>
          </div>
        </div>
      </div>

      <!-- Top SKUs -->
      <div class="rounded-xl border border-surface-200 bg-white p-4">
        <h2 class="text-base font-semibold text-surface-900 mb-2">Top SKUs by on-hand quantity</h2>
        <VueApexCharts v-if="top.length" type="bar" height="300" :options="topOpts" :series="topSeries" />
        <p v-else class="text-sm text-surface-500 py-8 text-center">No stock on hand.</p>
      </div>

    </template>
  </div>
</template>
