<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import LocationSelect from '@/components/pickers/LocationSelect.vue'
import { useMoveHandlingUnit } from './useHandlingUnits'
import type { HandlingUnitDto } from '@/types/handling-units'

const visible = defineModel<boolean>('visible', { default: false })
const props = defineProps<{ unit: HandlingUnitDto | null }>()

const toast = useToast()
const unitId = computed(() => props.unit?.id ?? '')
const move = useMoveHandlingUnit(unitId)

const destinationLocationId = ref<string | null>(null)
const fieldError = ref<string | null>(null)
const serverError = ref<string | null>(null)

const hasReserved = computed(
  () => (props.unit?.contents ?? []).some((c) => c.reserved > 0),
)

watch(visible, (open) => {
  if (open) {
    destinationLocationId.value = null
    fieldError.value = null
    serverError.value = null
  }
})

function submit() {
  fieldError.value = null
  serverError.value = null

  if (!props.unit) return

  if (!destinationLocationId.value) {
    fieldError.value = 'Select a destination location'
    return
  }
  if (destinationLocationId.value === props.unit.location?.id) {
    fieldError.value = 'Destination must differ from the current location'
    return
  }

  move.mutate(
    { destinationLocationId: destinationLocationId.value },
    {
      onSuccess: () => {
        toast.add({ severity: 'success', summary: 'Handling unit moved', life: 3000 })
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
  <Dialog v-model:visible="visible" modal header="Move handling unit" :style="{ width: '28rem' }">
    <div v-if="unit" class="flex flex-col gap-4">
      <div class="text-sm text-surface-600 rounded-lg bg-surface-50 p-3">
        <div class="font-medium text-surface-900">{{ unit.code }}</div>
        <div v-if="unit.location">
          From {{ unit.location.code }} · {{ unit.location.address }}
        </div>
        <div class="mt-2">
          Everything on the unit moves with it
          ({{ unit.contents.reduce((sum, c) => sum + c.onHand, 0) }} unit(s)).
        </div>
      </div>

      <Message v-if="hasReserved" severity="warn" :closable="false">
        Some stock on this unit is reserved for outbound work — the move will be rejected
        until those picks complete or are cancelled.
      </Message>

      <Message v-if="serverError" severity="error" :closable="false">
        {{ serverError }}
      </Message>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium text-surface-700">Destination location</label>
        <LocationSelect
          v-model="destinationLocationId"
          show-clear
          placeholder="Select destination"
        />
      </div>

      <small v-if="fieldError" class="text-red-500">{{ fieldError }}</small>
    </div>

    <template #footer>
      <Button label="Cancel" severity="secondary" text @click="visible = false" />
      <Button
        label="Move"
        icon="pi pi-arrows-alt"
        :loading="move.isPending.value"
        @click="submit"
      />
    </template>
  </Dialog>
</template>
