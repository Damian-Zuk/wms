<script setup lang="ts">
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import PickingStrategySelect from '@/components/pickers/PickingStrategySelect.vue'
import { useReplanLine } from './useStockOuts'
import type { StockOutLineDto } from '@/types/stock-outs'
import type { PickingStrategyType } from '@/types/enums'

const visible = defineModel<boolean>('visible', { default: false })
const props = defineProps<{ stockOutId: string; line: StockOutLineDto | null }>()

const toast = useToast()
const replan = useReplanLine(props.stockOutId)

const strategy = ref<PickingStrategyType>('Fefo')
const serverError = ref<string | null>(null)

// Seed the strategy from the line each time the dialog opens. A line whose locations
// were hand-edited is stamped 'Manual' (not a plannable strategy) — fall back to FEFO
// so re-running the planner takes the line back to an automatic plan.
watch(visible, (open) => {
  if (open && props.line) {
    strategy.value = props.line.strategy === 'Manual' ? 'Fefo' : props.line.strategy
    serverError.value = null
  }
})

function run() {
  if (!props.line) return
  serverError.value = null
  replan.mutate(
    { lineId: props.line.id, strategy: strategy.value },
    {
      onSuccess: () => {
        toast.add({
          severity: 'success',
          summary: 'Planner re-run',
          detail: 'Pick locations recomputed',
          life: 3000,
        })
        visible.value = false
      },
      onError: (err) => {
        serverError.value = err.message
      },
    },
  )
}
</script>

<template>
  <Dialog v-model:visible="visible" modal header="Run planner" :style="{ width: '30rem' }">
    <div v-if="line" class="flex flex-col gap-4">
      <div class="text-sm text-surface-600">
        Re-run the picking planner for
        <span class="font-medium text-surface-900">{{ line.product.sku }}</span>
        — {{ line.product.name }}. This discards the current pick locations and recomputes
        them from available inventory.
      </div>

      <Message v-if="serverError" severity="error" :closable="false">{{ serverError }}</Message>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium text-surface-700">Picking strategy</label>
        <PickingStrategySelect v-model="strategy" />
      </div>
    </div>

    <template #footer>
      <Button label="Cancel" severity="secondary" text @click="visible = false" />
      <Button
        label="Run planner"
        icon="pi pi-bolt"
        :loading="replan.isPending.value"
        @click="run"
      />
    </template>
  </Dialog>
</template>
