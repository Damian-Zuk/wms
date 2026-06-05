<script setup lang="ts">
import { computed, ref } from 'vue'
import VueApexCharts from 'vue3-apexcharts'
import Select from 'primevue/select'
import RefreshButton from '@/components/common/RefreshButton.vue'
import StatCard from './components/StatCard.vue'
import { useInboundOverview } from './useDashboard'
import { WINDOW_OPTIONS, areaOptions, horizontalBarOptions, formatHours, shortDate } from './dashboard-helpers'
import { putawayStrategyLabel } from '@/lib/enum-display'
import type { PutawayStrategyType } from '@/types/enums'

const days = ref(14)
const { data, isLoading, isFetching, isError, refetch } = useInboundOverview(days)

const status = computed(() => data.value?.statusCounts)
const progress = computed(() => data.value?.putawayProgress)
const series = computed(() => data.value?.receivedSeries ?? [])
const strategyMix = computed(() => data.value?.putawayStrategyMix ?? [])

const statusCards = computed(() => {
  const s = status.value
  if (!s) return []
  return [
    { title: 'Draft', value: s.draft, icon: 'pi pi-pencil', tone: 'default' as const },
    { title: 'In putaway', value: s.putaway, icon: 'pi pi-directions', tone: 'blue' as const },
    { title: 'Completed', value: s.completed, icon: 'pi pi-check-circle', tone: 'green' as const },
    { title: 'Cancelled', value: s.cancelled, icon: 'pi pi-times-circle', tone: 'red' as const },
  ]
})

const areaCategories = computed(() => series.value.map((p) => shortDate(p.date)))
const areaSeries = computed(() => [{ name: 'Received', data: series.value.map((p) => p.units) }])
const areaOpts = computed(() => areaOptions(areaCategories.value, '#22c55e'))

const strategyCategories = computed(() =>
  strategyMix.value.map((s) => putawayStrategyLabel[s.strategy as PutawayStrategyType] ?? s.strategy),
)
const strategySeries = computed(() => [{ name: 'Units', data: strategyMix.value.map((s) => s.units) }])
const strategyOpts = computed(() => horizontalBarOptions(strategyCategories.value, '#10b981'))
</script>

<template>
  <div class="flex flex-col gap-4">
    <div class="flex items-center justify-end gap-2">
      <Select v-model="days" :options="WINDOW_OPTIONS" option-label="label" option-value="value" class="w-44" />
      <RefreshButton :loading="isFetching" @click="() => refetch()" />
    </div>

    <div v-if="isError" class="rounded-lg border border-red-200 bg-red-50 text-red-700 p-4">
      Failed to load inbound data.
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

      <!-- Received series + putaway/ops stats -->
      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="xl:col-span-2 rounded-xl border border-surface-200 bg-white p-4">
          <h2 class="text-base font-semibold text-surface-900 mb-2">Units received</h2>
          <VueApexCharts type="area" height="300" :options="areaOpts" :series="areaSeries" />
        </div>

        <div class="rounded-xl border border-surface-200 bg-white p-4 flex flex-col gap-4">
          <div>
            <div class="flex items-end justify-between mb-1">
              <h2 class="text-base font-semibold text-surface-900">Putaway progress</h2>
              <span class="text-2xl font-semibold text-surface-900">{{ progress?.completionPercent ?? 0 }}%</span>
            </div>
            <div class="h-2.5 rounded-full bg-surface-200 overflow-hidden">
              <div
                class="h-full rounded-full bg-green-500 transition-all"
                :style="{ width: `${progress?.completionPercent ?? 0}%` }"
              />
            </div>
            <p class="text-xs text-surface-500 mt-1">
              {{ (progress?.placedUnits ?? 0).toLocaleString() }} /
              {{ (progress?.plannedUnits ?? 0).toLocaleString() }} units placed (in-progress)
            </p>
          </div>

          <div class="flex items-center justify-between border-t border-surface-100 pt-3">
            <span class="text-sm text-surface-500">Manual override rate</span>
            <span class="font-semibold text-surface-900">{{ data?.manualOverridePercent ?? 0 }}%</span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-sm text-surface-500">Avg receiving time</span>
            <span class="font-semibold text-surface-900">{{ formatHours(data?.avgReceivingHours) }}</span>
          </div>
        </div>
      </div>

      <!-- Putaway strategy mix -->
      <div class="rounded-xl border border-surface-200 bg-white p-4">
        <h2 class="text-base font-semibold text-surface-900 mb-2">Putaway strategy usage</h2>
        <VueApexCharts
          v-if="strategyMix.length"
          type="bar"
          height="280"
          :options="strategyOpts"
          :series="strategySeries"
        />
        <p v-else class="text-sm text-surface-500 py-8 text-center">No putaway placements yet.</p>
      </div>
    </template>
  </div>
</template>
