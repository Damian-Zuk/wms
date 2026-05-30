<script setup lang="ts">
import { watch } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import * as z from 'zod'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
import Button from 'primevue/button'
import { mapServerErrors } from '@/lib/form-errors'
import type { LocationFormValues } from '@/types/locations'
import type { LocationType, TemperatureZone } from '@/types/enums'

const props = defineProps<{
  mode: 'create' | 'edit'
  initialValues: LocationFormValues
  submitting?: boolean
  serverErrors?: Record<string, string[]>
}>()

const emit = defineEmits<{
  submit: [values: LocationFormValues]
  cancel: []
}>()

const typeOptions: { label: string; value: LocationType }[] = [
  { label: 'Storage', value: 'Storage' },
  { label: 'Quarantine', value: 'Quarantine' },
  { label: 'Returns', value: 'Returns' },
]

const zoneOptions: { label: string; value: TemperatureZone }[] = [
  { label: 'Ambient', value: 'Ambient' },
  { label: 'Chilled', value: 'Chilled' },
  { label: 'Frozen', value: 'Frozen' },
]

const schema = toTypedSchema(
  z.object({
    code: z.string().min(1, 'Code is required'),
    zone: z.string().min(1, 'Zone is required'),
    aisle: z.string().min(1, 'Aisle is required'),
    rack: z.string().min(1, 'Rack is required'),
    shelf: z.string().min(1, 'Shelf is required'),
    bin: z.string().min(1, 'Bin is required'),
    type: z.enum(['Storage', 'Quarantine', 'Returns']),
    temperatureZone: z.enum(['Ambient', 'Chilled', 'Frozen']),
    capacity: z
      .number()
      .int()
      .positive('Capacity must be greater than 0')
      .nullable(),
    description: z.string(),
    isMixedSkuAllowed: z.boolean(),
    isMixedLotAllowed: z.boolean(),
  }),
)

const { handleSubmit, defineField, errors, setErrors } = useForm({
  validationSchema: schema,
  initialValues: props.initialValues,
})

const [code, codeAttrs] = defineField('code')
const [zone, zoneAttrs] = defineField('zone')
const [aisle, aisleAttrs] = defineField('aisle')
const [rack, rackAttrs] = defineField('rack')
const [shelf, shelfAttrs] = defineField('shelf')
const [bin, binAttrs] = defineField('bin')
const [type] = defineField('type')
const [temperatureZone] = defineField('temperatureZone')
const [capacity] = defineField('capacity')
const [description, descriptionAttrs] = defineField('description')
const [isMixedSkuAllowed] = defineField('isMixedSkuAllowed')
const [isMixedLotAllowed] = defineField('isMixedLotAllowed')

watch(
  () => props.serverErrors,
  (serverErrors) => {
    setErrors(
      mapServerErrors(serverErrors) as Partial<Record<keyof LocationFormValues, string>>,
    )
  },
)

const onSubmit = handleSubmit((values) => emit('submit', values as LocationFormValues))
</script>

<template>
  <form class="flex flex-col gap-4 max-w-2xl" novalidate @submit="onSubmit">
    <div class="flex flex-col gap-1">
      <label for="code" class="text-sm font-medium text-surface-700">Code</label>
      <InputText id="code" v-model="code" v-bind="codeAttrs" fluid :invalid="!!errors.code" />
      <small v-if="errors.code" class="text-red-500">{{ errors.code }}</small>
    </div>

    <fieldset class="flex flex-col gap-2 border border-surface-200 rounded-lg p-4">
      <legend class="text-sm font-medium text-surface-700 px-1">Address</legend>
      <div class="grid grid-cols-2 sm:grid-cols-5 gap-3">
        <div class="flex flex-col gap-1">
          <label for="zone" class="text-xs text-surface-500">Zone</label>
          <InputText id="zone" v-model="zone" v-bind="zoneAttrs" fluid :invalid="!!errors.zone" />
        </div>
        <div class="flex flex-col gap-1">
          <label for="aisle" class="text-xs text-surface-500">Aisle</label>
          <InputText id="aisle" v-model="aisle" v-bind="aisleAttrs" fluid :invalid="!!errors.aisle" />
        </div>
        <div class="flex flex-col gap-1">
          <label for="rack" class="text-xs text-surface-500">Rack</label>
          <InputText id="rack" v-model="rack" v-bind="rackAttrs" fluid :invalid="!!errors.rack" />
        </div>
        <div class="flex flex-col gap-1">
          <label for="shelf" class="text-xs text-surface-500">Shelf</label>
          <InputText id="shelf" v-model="shelf" v-bind="shelfAttrs" fluid :invalid="!!errors.shelf" />
        </div>
        <div class="flex flex-col gap-1">
          <label for="bin" class="text-xs text-surface-500">Bin</label>
          <InputText id="bin" v-model="bin" v-bind="binAttrs" fluid :invalid="!!errors.bin" />
        </div>
      </div>
      <small
        v-if="errors.zone || errors.aisle || errors.rack || errors.shelf || errors.bin"
        class="text-red-500"
      >
        All address segments are required.
      </small>
    </fieldset>

    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <div class="flex flex-col gap-1">
        <label for="type" class="text-sm font-medium text-surface-700">Type</label>
        <Select
          input-id="type"
          v-model="type"
          :options="typeOptions"
          option-label="label"
          option-value="value"
          fluid
        />
      </div>
      <div class="flex flex-col gap-1">
        <label for="tempZone" class="text-sm font-medium text-surface-700">
          Temperature Zone
        </label>
        <Select
          input-id="tempZone"
          v-model="temperatureZone"
          :options="zoneOptions"
          option-label="label"
          option-value="value"
          fluid
        />
      </div>
    </div>

    <div class="flex flex-col gap-1">
      <label for="capacity" class="text-sm font-medium text-surface-700">Capacity</label>
      <InputNumber
        input-id="capacity"
        v-model="capacity"
        :min="1"
        :use-grouping="false"
        placeholder="Unlimited"
        fluid
        :invalid="!!errors.capacity"
      />
      <small v-if="errors.capacity" class="text-red-500">{{ errors.capacity }}</small>
      <small v-else class="text-surface-400">Leave empty for unlimited capacity.</small>
    </div>

    <div class="flex flex-col gap-1">
      <label for="description" class="text-sm font-medium text-surface-700">Description</label>
      <Textarea
        id="description"
        v-model="description"
        v-bind="descriptionAttrs"
        rows="2"
        fluid
      />
    </div>

    <div class="flex flex-col gap-3">
      <div class="flex items-center gap-3">
        <ToggleSwitch input-id="mixedSku" v-model="isMixedSkuAllowed" />
        <label for="mixedSku" class="text-sm text-surface-700">Allow mixed SKUs</label>
      </div>
      <div class="flex items-center gap-3">
        <ToggleSwitch input-id="mixedLot" v-model="isMixedLotAllowed" />
        <label for="mixedLot" class="text-sm text-surface-700">Allow mixed lots</label>
      </div>
    </div>

    <div class="flex gap-2 justify-end pt-2">
      <Button
        type="button"
        label="Cancel"
        severity="secondary"
        text
        @click="emit('cancel')"
      />
      <Button
        type="submit"
        :label="mode === 'create' ? 'Create' : 'Save'"
        icon="pi pi-check"
        :loading="submitting"
      />
    </div>
  </form>
</template>
