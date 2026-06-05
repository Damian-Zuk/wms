<script setup lang="ts">
import { computed, ref } from 'vue'
import type { LocationDto } from '@/types/locations'

const props = defineProps<{ location: LocationDto }>()

interface DimensionUsage {
  label: string
  used: number
  limit: number | null
  unit: string
  /** Utilisation as a 0–100 percentage, or null when this dimension is unlimited. */
  percent: number | null
}

/** Utilisation as a 0–100 percentage, or null when the dimension is unlimited. */
function utilisationPercent(used: number, limit: number | null): number | null {
  if (limit == null || limit === 0) return null
  return Math.round((used / limit) * 100)
}

const dimensions = computed<DimensionUsage[]>(() => {
  const l = props.location
  return [
    {
      label: 'Units',
      used: l.occupancy,
      limit: l.capacity,
      unit: 'units',
      percent: utilisationPercent(l.occupancy, l.capacity),
    },
    {
      label: 'Weight',
      used: l.weightOccupancy,
      limit: l.weightCapacity,
      unit: 'kg',
      percent: utilisationPercent(l.weightOccupancy, l.weightCapacity),
    },
    {
      label: 'Volume',
      used: l.volumeOccupancy,
      limit: l.volumeCapacity,
      unit: 'dm³',
      percent: utilisationPercent(l.volumeOccupancy, l.volumeCapacity),
    },
  ]
})

/** The fullest constrained dimension drives the bar — the most restrictive one wins. */
const overallPercent = computed<number | null>(() => {
  const percents = dimensions.value
    .map((d) => d.percent)
    .filter((p): p is number => p != null)
  return percents.length ? Math.max(...percents) : null
})

/** Bar fill is clamped to 100% so an over-capacity location does not overflow visually. */
const fillWidth = computed(() => Math.min(overallPercent.value ?? 0, 100))

const fillColor = computed(() => {
  const p = overallPercent.value ?? 0
  if (p >= 100) return 'bg-red-500'
  if (p >= 80) return 'bg-amber-500'
  return 'bg-emerald-500'
})

// Teleported tooltip: positioned with fixed coords from the trigger's rect so no
// table/overflow ancestor can clip it and it never affects the table layout (no flicker).
const TOOLTIP_WIDTH = 224 // px (w-56)
const anchor = ref<HTMLElement | null>(null)
const visible = ref(false)
const coords = ref({ top: 0, left: 0 })

function show() {
  const el = anchor.value
  if (!el) return
  const rect = el.getBoundingClientRect()
  const left = Math.min(
    Math.max(8, rect.left),
    window.innerWidth - TOOLTIP_WIDTH - 8,
  )
  coords.value = { top: rect.top, left }
  visible.value = true
}

function hide() {
  visible.value = false
}
</script>

<template>
  <div ref="anchor" class="w-full" @mouseenter="show" @mouseleave="hide">
    <!-- Progress bar -->
    <template v-if="overallPercent != null">
      <div class="flex items-center gap-2">
        <div class="h-2 flex-1 overflow-hidden rounded-full bg-surface-200">
          <div
            class="h-full rounded-full transition-all"
            :class="fillColor"
            :style="{ width: `${fillWidth}%` }"
          />
        </div>
        <span class="w-10 shrink-0 text-right text-xs tabular-nums text-surface-600">
          {{ overallPercent }}%
        </span>
      </div>
    </template>
    <span v-else class="text-surface-400">∞</span>

    <!-- Hover tooltip: occupancy percentage for all dimensions -->
    <Teleport to="body">
      <div
        v-if="visible"
        class="pointer-events-none fixed z-50 w-56 -translate-y-[calc(100%_+_8px)] rounded-lg border border-surface-200 bg-white p-3 text-xs shadow-lg"
        :style="{ top: `${coords.top}px`, left: `${coords.left}px` }"
      >
        <p class="mb-2 font-semibold text-surface-700">Capacity utilisation</p>
        <dl class="flex flex-col gap-1.5">
          <div
            v-for="d in dimensions"
            :key="d.label"
            class="flex items-center justify-between gap-2"
          >
            <dt class="text-surface-500">{{ d.label }}</dt>
            <dd class="text-surface-900 tabular-nums">
              <template v-if="d.percent != null">
                {{ d.used }} / {{ d.limit }} {{ d.unit }}
                <span class="ml-1 font-medium">({{ d.percent }}%)</span>
              </template>
              <template v-else> {{ d.used }} {{ d.unit }} · No limit </template>
            </dd>
          </div>
        </dl>
      </div>
    </Teleport>
  </div>
</template>
