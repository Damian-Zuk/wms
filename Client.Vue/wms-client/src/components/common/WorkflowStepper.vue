<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    steps: { value: string; label: string }[]
    current: string
    cancelled?: boolean
  }>(),
  { cancelled: false },
)

const currentIndex = computed(() => props.steps.findIndex((s) => s.value === props.current))

function state(index: number): 'done' | 'active' | 'todo' {
  if (index < currentIndex.value) return 'done'
  if (index === currentIndex.value) return 'active'
  return 'todo'
}
</script>

<template>
  <div class="flex items-center" :class="{ 'opacity-50': cancelled }">
    <template v-for="(step, i) in steps" :key="step.value">
      <div class="flex items-center gap-2">
        <span
          class="flex items-center justify-center w-7 h-7 rounded-full text-sm font-medium border"
          :class="{
            'bg-primary-500 border-primary-500 text-white': !cancelled && state(i) === 'active',
            'bg-primary-100 border-primary-200 text-primary-700': !cancelled && state(i) === 'done',
            'bg-surface-100 border-surface-200 text-surface-400': cancelled || state(i) === 'todo',
          }"
        >
          <i v-if="!cancelled && state(i) === 'done'" class="pi pi-check text-xs" />
          <span v-else>{{ i + 1 }}</span>
        </span>
        <span
          class="text-sm whitespace-nowrap"
          :class="{
            'font-semibold text-surface-900': !cancelled && state(i) === 'active',
            'text-surface-700': !cancelled && state(i) === 'done',
            'text-surface-400': cancelled || state(i) === 'todo',
          }"
        >
          {{ step.label }}
        </span>
      </div>
      <div
        v-if="i < steps.length - 1"
        class="flex-1 h-px bg-surface-200 mx-3 min-w-8"
      />
    </template>
  </div>
</template>
