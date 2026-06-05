<script setup lang="ts">
import { computed, ref } from 'vue'
import VueApexCharts from 'vue3-apexcharts'
import Chart from 'primevue/chart'
import Select from 'primevue/select'
import RefreshButton from '@/components/common/RefreshButton.vue'
import StatCard from './components/StatCard.vue'
import { useOutboundOverview } from './useDashboard'
import { WINDOW_OPTIONS, areaOptions, horizontalBarOptions, formatHours, shortDate } from './dashboard-helpers'
import { pickingStrategyLabel } from '@/lib/enum-display'
import type { PickingStrategyType } from '@/types/enums'

const days = ref(14)
const { data, isLoading, isFetching, isError, refetch } = useOutboundOverview(days)

const status = computed(() => data.value?.statusCounts)
const progress = computed(() => data.value?.pickProgress)
const cancellations = computed(() => data.value?.cancellations)
const series = computed(() => data.value?.shippedSeries ?? [])
const pickMix = computed(() => data.value?.pickingStrategyMix ?? [])
const top = computed(() => data.value?.topPickedProducts ?? [])

const statusCards = computed(() => {
  const s = status.value
  if (!s) return []
  return [
    { title: 'Draft', value: s.draft, icon: 'pi pi-pencil', tone: 'default' as const },
    { title: 'Picking', value: s.picking, icon: 'pi pi-shopping-cart', tone: 'blue' as const },
    { title: 'Completed', value: s.completed, icon: 'pi pi-check-circle', tone: 'green' as const },
    { title: 'Cancelled', value: s.cancelled, icon: 'pi pi-times-circle', tone: 'red' as const },
  ]
})

const completionRate = computed(() => {
  const s = status.value
  if (!s) return null
  const terminal = s.completed + s.cancelled
  return terminal > 0 ? Math.round((s.completed / terminal) * 100) : null
})

const areaCategories = computed(() => series.value.map((p) => shortDate(p.date)))
const areaSeries = computed(() => [{ name: 'Shipped', data: series.value.map((p) => p.units) }])
const areaOpts = computed(() => areaOptions(areaCategories.value, '#3b82f6'))

// Picking strategy split (doughnut via Chart.js)
const donutData = computed(() => ({
  labels: pickMix.value.map((s) => pickingStrategyLabel[s.strategy as PickingStrategyType] ?? s.strategy),
  datasets: [
    {
      data: pickMix.value.map((s) => s.units),
      backgroundColor: ['#3b82f6', '#f59e0b', '#22c55e', '#a855f7'],
      borderWidth: 0,
    },
  ],
}))
const donutOptions = {
  cutout: '65%',
  maintainAspectRatio: false,
  plugins: { legend: { position: 'bottom', labels: { usePointStyle: true } } },
}

// Top picked products (horizontal bar)
const topCategories = computed(() => top.value.map((p) => p.sku))
const topSeries = computed(() => [{ name: 'Units', data: top.value.map((p) => p.units) }])
const topOpts = computed(() => horizontalBarOptions(topCategories.value, '#3b82f6'))
</script>

<template>
  <div class="flex flex-col gap-4">
    <div class="flex items-center justify-end gap-2">
      <Select v-model="days" :options="WINDOW_OPTIONS" option-label="label" option-value="value" class="w-44" />
      <RefreshButton :loading="isFetching" @click="() => refetch()" />
    </div>

    <div v-if="isError" class="rounded-lg border border-red-200 bg-red-50 text-red-700 p-4">
      Failed to load outbound data.
    </div>

    <div v-else-if="isLoading" class="grid grid-cols-2 lg:grid-cols-4 gap-4">
      <div v-for="n in 4" :key="n" class="h-28 rounded-xl border border-surface-200 bg-surface-100 animate-pulse" />
    </div>

    <template v-else>
      <!-- Status funnel -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          v-for="card in statusCards"
          :key="card.title"
          :title="card.title"
          :value="card.value"
          :icon="card.icon"
          :tone="card.tone"
        />
      </div>

      <!-- Shipped series + pick/ops stats -->
      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="xl:col-span-2 rounded-xl border border-surface-200 bg-white p-4">
          <h2 class="text-base font-semibold text-surface-900 mb-2">Units shipped</h2>
          <VueApexCharts type="area" height="300" :options="areaOpts" :series="areaSeries" />
        </div>

        <div class="rounded-xl border border-surface-200 bg-white p-4 flex flex-col gap-4">
          <div>
            <div class="flex items-end justify-between mb-1">
              <h2 class="text-base font-semibold text-surface-900">Pick progress</h2>
              <span class="text-2xl font-semibold text-surface-900">{{ progress?.completionPercent ?? 0 }}%</span>
            </div>
            <div class="h-2.5 rounded-full bg-surface-200 overflow-hidden">
              <div
                class="h-full rounded-full bg-blue-500 transition-all"
                :style="{ width: `${progress?.completionPercent ?? 0}%` }"
              />
            </div>
            <p class="text-xs text-surface-500 mt-1">
              {{ (progress?.pickedUnits ?? 0).toLocaleString() }} /
              {{ (progress?.plannedUnits ?? 0).toLocaleString() }} units picked (in-progress)
            </p>
          </div>

          <div class="flex items-center justify-between border-t border-surface-100 pt-3">
            <span class="text-sm text-surface-500">Completion rate</span>
            <span class="font-semibold text-surface-900">{{ completionRate == null ? '—' : `${completionRate}%` }}</span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-sm text-surface-500">Avg fulfillment time</span>
            <span class="font-semibold text-surface-900">{{ formatHours(data?.avgFulfillmentHours) }}</span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-sm text-surface-500">Cancelled</span>
            <span class="text-sm text-surface-700">
              {{ cancellations?.fromDraft ?? 0 }} draft · {{ cancellations?.fromPicking ?? 0 }} picking
            </span>
          </div>
        </div>
      </div>

      <!-- Strategy split + top movers -->
      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="rounded-xl border border-surface-200 bg-white p-4 flex flex-col">
          <h2 class="text-base font-semibold text-surface-900 mb-2">Picking strategy</h2>
          <div v-if="pickMix.length" class="flex-1 flex items-center justify-center" style="height: 280px">
            <Chart type="doughnut" :data="donutData" :options="donutOptions" class="w-full h-full" />
          </div>
          <p v-else class="text-sm text-surface-500 py-8 text-center">No picks yet.</p>
        </div>

        <div class="xl:col-span-2 rounded-xl border border-surface-200 bg-white p-4">
          <h2 class="text-base font-semibold text-surface-900 mb-2">Top picked products</h2>
          <VueApexCharts
            v-if="top.length"
            type="bar"
            height="280"
            :options="topOpts"
            :series="topSeries"
          />
          <p v-else class="text-sm text-surface-500 py-8 text-center">No outbound movements in this window.</p>
        </div>
      </div>
    </template>
  </div>
</template>
