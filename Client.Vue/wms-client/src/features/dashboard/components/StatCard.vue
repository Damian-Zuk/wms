<script setup lang="ts">
import { computed } from 'vue'

type Tone = 'default' | 'green' | 'blue' | 'amber' | 'red'

const props = withDefaults(
  defineProps<{
    title: string
    value: string | number
    icon: string
    tone?: Tone
    /** Change vs the comparison period; renders an arrow + colour when provided. */
    delta?: number
    deltaLabel?: string
  }>(),
  { tone: 'default' },
)

const toneClasses: Record<Tone, string> = {
  default: 'bg-surface-100 text-surface-600',
  green: 'bg-green-100 text-green-600',
  blue: 'bg-blue-100 text-blue-600',
  amber: 'bg-amber-100 text-amber-600',
  red: 'bg-red-100 text-red-600',
}

const displayValue = computed(() =>
  typeof props.value === 'number' ? props.value.toLocaleString() : props.value,
)

const hasDelta = computed(() => props.delta !== undefined)

const deltaIcon = computed(() => {
  if (!props.delta) return 'pi pi-minus'
  return props.delta > 0 ? 'pi pi-arrow-up' : 'pi pi-arrow-down'
})

const deltaColor = computed(() => {
  if (!props.delta) return 'text-surface-400'
  return props.delta > 0 ? 'text-green-600' : 'text-red-600'
})

const deltaText = computed(() => {
  const d = props.delta ?? 0
  return `${d > 0 ? '+' : ''}${d.toLocaleString()}`
})
</script>

<template>
  <div class="rounded-xl border border-surface-200 bg-white p-4 flex flex-col gap-2">
    <div class="flex items-start justify-between gap-2">
      <span class="text-sm text-surface-500">{{ title }}</span>
      <span
        class="w-9 h-9 rounded-lg flex items-center justify-center shrink-0"
        :class="toneClasses[tone]"
      >
        <i :class="icon" />
      </span>
    </div>
    <div class="text-2xl font-semibold text-surface-900 leading-tight">{{ displayValue }}</div>
    <div v-if="hasDelta" class="text-xs flex items-center gap-1" :class="deltaColor">
      <i :class="deltaIcon" class="text-[0.7rem]" />
      <span>{{ deltaText }}</span>
      <span v-if="deltaLabel" class="text-surface-400">{{ deltaLabel }}</span>
    </div>
  </div>
</template>
