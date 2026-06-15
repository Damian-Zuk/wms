<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import LocationSelect from '@/components/pickers/LocationSelect.vue'
import { useCreateHandlingUnit } from './useHandlingUnits'
import { handlingUnitTypeLabel } from '@/lib/enum-display'
import type { HandlingUnitType } from '@/types/enums'

const visible = defineModel<boolean>('visible', { default: false })

const toast = useToast()
const create = useCreateHandlingUnit()

const locationId = ref<string | null>(null)
const type = ref<HandlingUnitType>('Pallet')
const code = ref('')
const fieldError = ref<string | null>(null)
const serverError = ref<string | null>(null)

const typeOptions = computed(() =>
  (Object.keys(handlingUnitTypeLabel) as HandlingUnitType[]).map((t) => ({
    label: handlingUnitTypeLabel[t],
    value: t,
  })),
)

watch(visible, (open) => {
  if (open) {
    locationId.value = null
    type.value = 'Pallet'
    code.value = ''
    fieldError.value = null
    serverError.value = null
  }
})

function submit() {
  fieldError.value = null
  serverError.value = null

  if (!locationId.value) {
    fieldError.value = 'Select a location'
    return
  }

  create.mutate(
    { locationId: locationId.value, type: type.value, code: code.value.trim() || null },
    {
      onSuccess: () => {
        toast.add({ severity: 'success', summary: 'Handling unit created', life: 3000 })
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
  <Dialog v-model:visible="visible" modal header="New handling unit" :style="{ width: '26rem' }">
    <div class="flex flex-col gap-4">
      <Message v-if="serverError" severity="error" :closable="false">
        {{ serverError }}
      </Message>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium text-surface-700">Type</label>
        <Select
          v-model="type"
          :options="typeOptions"
          option-label="label"
          option-value="value"
          fluid
        />
      </div>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium text-surface-700">Location</label>
        <LocationSelect v-model="locationId" placeholder="Where the unit stands" />
      </div>

      <div class="flex flex-col gap-1">
        <label for="hu-code" class="text-sm font-medium text-surface-700">
          Code <span class="text-surface-400">(optional — generated when empty)</span>
        </label>
        <InputText id="hu-code" v-model="code" placeholder="e.g. PAL-0042" maxlength="50" fluid />
      </div>

      <small v-if="fieldError" class="text-red-500">{{ fieldError }}</small>
    </div>

    <template #footer>
      <Button label="Cancel" severity="secondary" text @click="visible = false" />
      <Button
        label="Create"
        icon="pi pi-plus"
        :loading="create.isPending.value"
        @click="submit"
      />
    </template>
  </Dialog>
</template>
