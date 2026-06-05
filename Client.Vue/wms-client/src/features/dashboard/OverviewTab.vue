<script setup lang="ts">
import { computed, ref } from 'vue'
import VueApexCharts from 'vue3-apexcharts'
import type { ApexOptions } from 'apexcharts'
import Chart from 'primevue/chart'
import Select from 'primevue/select'
import RefreshButton from '@/components/common/RefreshButton.vue'
import StatCard from './components/StatCard.vue'
import { useDashboardOverview } from './useDashboard'

const days = ref(14)
const windowOptions = [
  { label: 'Last 7 days', value: 7 },
  { label: 'Last 14 days', value: 14 },
  { label: 'Last 30 days', value: 30 },
  { label: 'Last 90 days', value: 90 },
]

const { data, isLoading, isFetching, isError, refetch } = useDashboardOverview(days)

const inv = computed(() => data.value?.inventory)
const flow = computed(() => data.value?.flowToday)
const wo = computed(() => data.value?.workOrders)
const throughput = computed(() => data.value?.throughput ?? [])

function shortDate(iso: string): string {
  const d = new Date(`${iso}T00:00:00`)
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}

const cards = computed(() => {
  const i = inv.value
  const f = flow.value
  const w = wo.value
  if (!i || !f || !w) return []
  return [
    { title: 'On-hand units', value: i.onHandUnits, icon: 'pi pi-database', tone: 'default' as const },
    { title: 'Available units', value: i.availableUnits, icon: 'pi pi-check-circle', tone: 'green' as const },
    { title: 'Distinct SKUs', value: i.distinctSkus, icon: 'pi pi-box', tone: 'default' as const },
    {
      title: 'Occupied / active locations',
      value: `${i.occupiedLocations} / ${i.activeLocations}`,
      icon: 'pi pi-map-marker',
      tone: 'default' as const,
    },
    {
      title: 'Received today',
      value: f.receivedToday,
      icon: 'pi pi-download',
      tone: 'green' as const,
      delta: f.receivedToday - f.receivedPrevDay,
      deltaLabel: 'vs yesterday',
    },
    {
      title: 'Shipped today',
      value: f.shippedToday,
      icon: 'pi pi-upload',
      tone: 'blue' as const,
      delta: f.shippedToday - f.shippedPrevDay,
      deltaLabel: 'vs yesterday',
    },
    { title: 'Open stock-ins', value: w.draftStockIns + w.putawayStockIns, icon: 'pi pi-inbox', tone: 'default' as const },
    { title: 'Open stock-outs', value: w.draftStockOuts + w.pickingStockOuts, icon: 'pi pi-send', tone: 'default' as const },
  ]
})

const alertItems = computed(() => {
  const a = data.value?.alerts
  if (!a) return []
  return [
    { label: 'Expiring within 30 days', value: a.expiringSoonLots, icon: 'pi pi-clock', tone: 'amber' as const },
    { label: 'Expired stock on hand', value: a.expiredOnHandLots, icon: 'pi pi-exclamation-triangle', tone: 'red' as const },
    { label: 'Blocked locations', value: a.blockedLocations, icon: 'pi pi-ban', tone: 'red' as const },
    { label: 'Inactive locations', value: a.inactiveLocations, icon: 'pi pi-power-off', tone: 'default' as const },
  ]
})

const alertTone: Record<'amber' | 'red' | 'default', string> = {
  amber: 'bg-amber-50 text-amber-700 border-amber-200',
  red: 'bg-red-50 text-red-700 border-red-200',
  default: 'bg-surface-50 text-surface-600 border-surface-200',
}

// --- Throughput area chart (ApexCharts) ---
const areaSeries = computed(() => [
  { name: 'Received', data: throughput.value.map((p) => p.received) },
  { name: 'Shipped', data: throughput.value.map((p) => p.shipped) },
])

const areaOptions = computed<ApexOptions>(() => ({
  chart: {
    type: 'area',
    toolbar: { show: false },
    zoom: { enabled: false },
    fontFamily: 'inherit',
    animations: { speed: 400 },
  },
  colors: ['#22c55e', '#3b82f6'],
  dataLabels: { enabled: false },
  stroke: { curve: 'smooth', width: 2 },
  fill: {
    type: 'gradient',
    gradient: { shadeIntensity: 1, opacityFrom: 0.35, opacityTo: 0.05, stops: [0, 90, 100] },
  },
  xaxis: {
    categories: throughput.value.map((p) => shortDate(p.date)),
    tickAmount: Math.min(7, throughput.value.length),
    axisBorder: { show: false },
    axisTicks: { show: false },
    labels: { rotate: 0, hideOverlappingLabels: true, style: { colors: '#94a3b8' } },
  },
  yaxis: {
    labels: { formatter: (val: number) => Math.round(val).toString(), style: { colors: '#94a3b8' } },
  },
  legend: { position: 'top', horizontalAlign: 'right' },
  grid: { borderColor: '#e5e7eb', strokeDashArray: 4 },
  tooltip: { shared: true },
}))

// --- Available vs Reserved doughnut (PrimeVue Chart / Chart.js) ---
const donutData = computed(() => ({
  labels: ['Available', 'Reserved'],
  datasets: [
    {
      data: [inv.value?.availableUnits ?? 0, inv.value?.reservedUnits ?? 0],
      backgroundColor: ['#3b82f6', '#f59e0b'],
      hoverBackgroundColor: ['#2563eb', '#d97706'],
      borderWidth: 0,
    },
  ],
}))

const donutOptions = {
  cutout: '65%',
  maintainAspectRatio: false,
  plugins: { legend: { position: 'bottom', labels: { usePointStyle: true } } },
}
</script>

<template>
  <div class="flex flex-col gap-4">
    <!-- Toolbar -->
    <div class="flex items-center justify-end gap-2">
      <Select
        v-model="days"
        :options="windowOptions"
        option-label="label"
        option-value="value"
        class="w-44"
      />
      <RefreshButton :loading="isFetching" @click="() => refetch()" />
    </div>

    <div v-if="isError" class="rounded-lg border border-red-200 bg-red-50 text-red-700 p-4">
      Failed to load dashboard data.
    </div>

    <!-- Loading skeleton -->
    <div v-else-if="isLoading" class="grid grid-cols-2 lg:grid-cols-4 gap-4">
      <div
        v-for="n in 8"
        :key="n"
        class="h-28 rounded-xl border border-surface-200 bg-surface-100 animate-pulse"
      />
    </div>

    <template v-else>
      <!-- KPI cards -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          v-for="card in cards"
          :key="card.title"
          :title="card.title"
          :value="card.value"
          :icon="card.icon"
          :tone="card.tone"
          :delta="card.delta"
          :delta-label="card.deltaLabel"
        />
      </div>

      <!-- Charts row -->
      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="xl:col-span-2 rounded-xl border border-surface-200 bg-white p-4">
          <h2 class="text-base font-semibold text-surface-900 mb-2">Receiving vs Shipping</h2>
          <VueApexCharts type="area" height="320" :options="areaOptions" :series="areaSeries" />
        </div>

        <div class="rounded-xl border border-surface-200 bg-white p-4 flex flex-col">
          <h2 class="text-base font-semibold text-surface-900 mb-2">Stock commitment</h2>
          <div class="flex-1 min-h-0 flex items-center justify-center" style="height: 320px">
            <Chart type="doughnut" :data="donutData" :options="donutOptions" class="w-full h-full" />
          </div>
        </div>
      </div>

      <!-- Alerts -->
      <div class="rounded-xl border border-surface-200 bg-white p-4">
        <h2 class="text-base font-semibold text-surface-900 mb-3">Alerts</h2>
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-3">
          <div
            v-for="item in alertItems"
            :key="item.label"
            class="flex items-center gap-3 rounded-lg border p-3"
            :class="item.value > 0 ? alertTone[item.tone] : alertTone.default"
          >
            <i :class="item.icon" class="text-lg" />
            <div class="flex flex-col">
              <span class="text-xl font-semibold leading-none">{{ item.value }}</span>
              <span class="text-xs mt-1 opacity-80">{{ item.label }}</span>
            </div>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
