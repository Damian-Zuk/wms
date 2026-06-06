<script setup lang="ts">
import { computed } from 'vue'
import VueApexCharts from 'vue3-apexcharts'
import RefreshButton from '@/components/common/RefreshButton.vue'
import StatCard from './components/StatCard.vue'
import { useCapacityOverview } from './useDashboard'
import { radialBarOptions, donutOptions, horizontalBarOptions } from './dashboard-helpers'

const { data, isLoading, isFetching, isError, refetch } = useCapacityOverview()

const summary = computed(() => data.value?.summary)
const util = computed(() => data.value?.utilization)
const fullest = computed(() => data.value?.fullestLocations ?? [])
const byZone = computed(() => data.value?.byZone ?? [])
const blocked = computed(() => data.value?.blockedLocations ?? [])

const cards = computed(() => {
  const s = summary.value
  if (!s) return []
  return [
    { title: 'Total locations', value: s.total, icon: 'pi pi-map-marker', tone: 'default' as const },
    { title: 'Occupied', value: s.occupied, icon: 'pi pi-inbox', tone: 'blue' as const },
    { title: 'Empty', value: s.empty, icon: 'pi pi-stop', tone: 'default' as const },
    { title: 'Blocked', value: s.blocked, icon: 'pi pi-ban', tone: 'red' as const },
  ]
})

// Utilization gauges
const unitsOpts = radialBarOptions('Units', '#3b82f6')
const weightOpts = radialBarOptions('Weight', '#14b8a6')
const volumeOpts = radialBarOptions('Volume', '#8b5cf6')

function capCaption(used: number, cap: number, unit: string): string {
  if (cap <= 0) return 'No limits set'
  return `${Math.round(used).toLocaleString()} / ${Math.round(cap).toLocaleString()} ${unit}`
}

// Occupied vs empty doughnut
const occLabels = ['Occupied', 'Empty']
const occSeries = computed(() => [summary.value?.occupied ?? 0, summary.value?.empty ?? 0])
const occOpts = donutOptions(occLabels, ['#3b82f6', '#cbd5e1'])

// Fill by zone
const zoneCategories = computed(() => byZone.value.map((z) => z.zone))
const zoneSeries = computed(() => [{ name: 'Avg fill %', data: byZone.value.map((z) => z.fillPercent) }])
const zoneOpts = computed(() => horizontalBarOptions(zoneCategories.value, '#0ea5e9'))

function fillTone(pct: number): string {
  if (pct >= 90) return 'bg-red-500'
  if (pct >= 70) return 'bg-amber-500'
  return 'bg-green-500'
}
</script>

<template>
  <div class="flex flex-col gap-4">
    <div class="flex items-center justify-end">
      <RefreshButton :loading="isFetching" @click="() => refetch()" />
    </div>

    <div v-if="isError" class="rounded-lg border border-red-200 bg-red-50 text-red-700 p-4">
      Failed to load capacity data.
    </div>

    <div v-else-if="isLoading" class="grid grid-cols-2 lg:grid-cols-4 gap-4">
      <div v-for="n in 4" :key="n" class="h-28 rounded-xl border border-surface-200 bg-surface-100 animate-pulse" />
    </div>

    <template v-else>
      <!-- KPIs -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          v-for="card in cards"
          :key="card.title"
          :title="card.title"
          :value="card.value"
          :icon="card.icon"
          :tone="card.tone"
        />
      </div>

      <!-- Utilization gauges -->
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div class="rounded-xl border border-surface-200 bg-white p-4 flex flex-col items-center">
          <h2 class="text-base font-semibold text-surface-900 self-start mb-1">Units utilization</h2>
          <VueApexCharts type="radialBar" height="220" :options="unitsOpts" :series="[util?.unitsPercent ?? 0]" />
          <p class="text-xs text-surface-500">{{ capCaption(util?.usedUnits ?? 0, util?.unitsCapacity ?? 0, 'units') }}</p>
        </div>
        <div class="rounded-xl border border-surface-200 bg-white p-4 flex flex-col items-center">
          <h2 class="text-base font-semibold text-surface-900 self-start mb-1">Weight utilization</h2>
          <VueApexCharts type="radialBar" height="220" :options="weightOpts" :series="[util?.weightPercent ?? 0]" />
          <p class="text-xs text-surface-500">{{ capCaption(util?.usedWeight ?? 0, util?.weightCapacity ?? 0, 'kg') }}</p>
        </div>
        <div class="rounded-xl border border-surface-200 bg-white p-4 flex flex-col items-center">
          <h2 class="text-base font-semibold text-surface-900 self-start mb-1">Volume utilization</h2>
          <VueApexCharts type="radialBar" height="220" :options="volumeOpts" :series="[util?.volumePercent ?? 0]" />
          <p class="text-xs text-surface-500">{{ capCaption(util?.usedVolume ?? 0, util?.volumeCapacity ?? 0, 'dm³') }}</p>
        </div>
      </div>

      <!-- Fullest locations + occupancy -->
      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="xl:col-span-2 rounded-xl border border-surface-200 bg-white p-4">
          <h2 class="text-base font-semibold text-surface-900 mb-3">Fullest locations</h2>
          <div v-if="fullest.length" class="flex flex-col gap-3">
            <div v-for="loc in fullest" :key="loc.locationId" class="flex items-center gap-3">
              <div class="w-40 shrink-0">
                <div class="font-medium text-surface-900 text-sm">{{ loc.code }}</div>
                <div class="text-xs text-surface-500">{{ loc.address }}</div>
              </div>
              <div class="flex-1 h-2.5 rounded-full bg-surface-200 overflow-hidden">
                <div
                  class="h-full rounded-full transition-all"
                  :class="fillTone(loc.fillPercent)"
                  :style="{ width: `${Math.min(loc.fillPercent, 100)}%` }"
                />
              </div>
              <span class="w-12 text-right text-sm font-semibold text-surface-700">{{ loc.fillPercent }}%</span>
            </div>
          </div>
          <p v-else class="text-sm text-surface-500 py-8 text-center">No locations with capacity limits.</p>
        </div>

        <div class="rounded-xl border border-surface-200 bg-white p-4 flex flex-col">
          <h2 class="text-base font-semibold text-surface-900 mb-2">Occupancy</h2>
          <div class="flex-1 flex items-center justify-center" style="height: 260px">
            <VueApexCharts type="donut" height="260" :options="occOpts" :series="occSeries" class="w-full" />
          </div>
        </div>
      </div>

      <!-- By zone + blocked -->
      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="xl:col-span-2 rounded-xl border border-surface-200 bg-white p-4">
          <h2 class="text-base font-semibold text-surface-900 mb-2">Average fill by zone</h2>
          <VueApexCharts v-if="byZone.length" type="bar" height="280" :options="zoneOpts" :series="zoneSeries" />
          <p v-else class="text-sm text-surface-500 py-8 text-center">No locations.</p>
        </div>

        <div class="rounded-xl border border-surface-200 bg-white p-4">
          <h2 class="text-base font-semibold text-surface-900 mb-3">Blocked locations</h2>
          <div v-if="blocked.length" class="flex flex-col gap-2 max-h-72 overflow-auto">
            <div
              v-for="loc in blocked"
              :key="loc.locationId"
              class="rounded-lg border border-red-200 bg-red-50 p-3"
            >
              <div class="flex items-center justify-between">
                <span class="font-medium text-surface-900 text-sm">{{ loc.code }}</span>
                <span class="text-xs text-surface-500">{{ loc.address }}</span>
              </div>
              <p v-if="loc.reason" class="text-xs text-red-700 mt-1">{{ loc.reason }}</p>
            </div>
          </div>
          <p v-else class="text-sm text-surface-500 py-8 text-center">No blocked locations.</p>
        </div>
      </div>
    </template>
  </div>
</template>
