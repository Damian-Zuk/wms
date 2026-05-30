<script setup lang="ts">
import { watch } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import * as z from 'zod'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import Select from 'primevue/select'
import Button from 'primevue/button'
import LocationMultiSelect from '@/components/pickers/LocationMultiSelect.vue'
import { mapServerErrors } from '@/lib/form-errors'
import type { ProductFormValues } from '@/types/products'
import type { TemperatureZone } from '@/types/enums'

const props = defineProps<{
  mode: 'create' | 'edit'
  initialValues: ProductFormValues
  submitting?: boolean
  serverErrors?: Record<string, string[]>
}>()

const emit = defineEmits<{
  submit: [values: ProductFormValues]
  cancel: []
}>()

const zoneOptions: { label: string; value: TemperatureZone }[] = [
  { label: 'Ambient', value: 'Ambient' },
  { label: 'Chilled', value: 'Chilled' },
  { label: 'Frozen', value: 'Frozen' },
]

const schema = toTypedSchema(
  z.object({
    sku:
      props.mode === 'create'
        ? z.string().min(1, 'SKU is required')
        : z.string(),
    name: z.string().min(1, 'Name is required'),
    description: z.string(),
    requiredTemperatureZone: z.enum(['Ambient', 'Chilled', 'Frozen']),
    preferredLocationIds: z.array(z.string()),
  }),
)

const { handleSubmit, defineField, errors, setErrors } = useForm({
  validationSchema: schema,
  initialValues: props.initialValues,
})

const [sku, skuAttrs] = defineField('sku')
const [name, nameAttrs] = defineField('name')
const [description, descriptionAttrs] = defineField('description')
const [requiredTemperatureZone] = defineField('requiredTemperatureZone')
const [preferredLocationIds] = defineField('preferredLocationIds')

// Map server-side (PascalCase) field errors onto the form fields (camelCase).
watch(
  () => props.serverErrors,
  (serverErrors) => {
    setErrors(
      mapServerErrors(serverErrors) as Partial<Record<keyof ProductFormValues, string>>,
    )
  },
)

const onSubmit = handleSubmit((values) => emit('submit', values as ProductFormValues))
</script>

<template>
  <form class="flex flex-col gap-4 max-w-2xl" novalidate @submit="onSubmit">
    <div class="flex flex-col gap-1">
      <label for="sku" class="text-sm font-medium text-surface-700">SKU</label>
      <InputText
        id="sku"
        v-model="sku"
        v-bind="skuAttrs"
        :disabled="mode === 'edit'"
        fluid
        :invalid="!!errors.sku"
      />
      <small v-if="mode === 'edit'" class="text-surface-400">SKU cannot be changed.</small>
      <small v-if="errors.sku" class="text-red-500">{{ errors.sku }}</small>
    </div>

    <div class="flex flex-col gap-1">
      <label for="name" class="text-sm font-medium text-surface-700">Name</label>
      <InputText
        id="name"
        v-model="name"
        v-bind="nameAttrs"
        fluid
        :invalid="!!errors.name"
      />
      <small v-if="errors.name" class="text-red-500">{{ errors.name }}</small>
    </div>

    <div class="flex flex-col gap-1">
      <label for="description" class="text-sm font-medium text-surface-700">
        Description
      </label>
      <Textarea
        id="description"
        v-model="description"
        v-bind="descriptionAttrs"
        rows="3"
        fluid
        :invalid="!!errors.description"
      />
      <small v-if="errors.description" class="text-red-500">{{ errors.description }}</small>
    </div>

    <div class="flex flex-col gap-1">
      <label for="zone" class="text-sm font-medium text-surface-700">
        Required Temperature Zone
      </label>
      <Select
        input-id="zone"
        v-model="requiredTemperatureZone"
        :options="zoneOptions"
        option-label="label"
        option-value="value"
        fluid
      />
    </div>

    <div class="flex flex-col gap-1">
      <label class="text-sm font-medium text-surface-700">Preferred Locations</label>
      <LocationMultiSelect v-model="preferredLocationIds" />
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
