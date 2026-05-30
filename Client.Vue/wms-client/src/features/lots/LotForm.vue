<script setup lang="ts">
import { watch } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import * as z from 'zod'
import InputText from 'primevue/inputtext'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'
import ProductSelect from '@/components/pickers/ProductSelect.vue'
import { mapServerErrors } from '@/lib/form-errors'
import type { LotFormValues } from '@/types/lots'

const props = defineProps<{
  mode: 'create' | 'edit'
  initialValues: LotFormValues
  submitting?: boolean
  serverErrors?: Record<string, string[]>
}>()

const emit = defineEmits<{
  submit: [values: LotFormValues]
  cancel: []
}>()

const schema = toTypedSchema(
  z
    .object({
      number:
        props.mode === 'create'
          ? z.string().min(1, 'Lot number is required')
          : z.string(),
      productId:
        props.mode === 'create'
          ? z.string().min(1, 'Product is required')
          : z.string(),
      manufactureDate: z.date().nullable(),
      expirationDate: z.date().nullable(),
    })
    .refine(
      (v) =>
        !(v.manufactureDate && v.expirationDate) ||
        v.expirationDate >= v.manufactureDate,
      {
        message: 'Expiration date must be on or after the manufacture date',
        path: ['expirationDate'],
      },
    ),
)

const { handleSubmit, defineField, errors, setErrors } = useForm({
  validationSchema: schema,
  initialValues: props.initialValues,
})

const [number, numberAttrs] = defineField('number')
const [productId] = defineField('productId')
const [manufactureDate] = defineField('manufactureDate')
const [expirationDate] = defineField('expirationDate')

watch(
  () => props.serverErrors,
  (serverErrors) => {
    setErrors(
      mapServerErrors(serverErrors) as Partial<Record<keyof LotFormValues, string>>,
    )
  },
)

const onSubmit = handleSubmit((values) => emit('submit', values as LotFormValues))
</script>

<template>
  <form class="flex flex-col gap-4 max-w-2xl" novalidate @submit="onSubmit">
    <div class="flex flex-col gap-1">
      <label for="number" class="text-sm font-medium text-surface-700">Lot Number</label>
      <InputText
        id="number"
        v-model="number"
        v-bind="numberAttrs"
        :disabled="mode === 'edit'"
        fluid
        :invalid="!!errors.number"
      />
      <small v-if="mode === 'edit'" class="text-surface-400">
        Lot number cannot be changed.
      </small>
      <small v-if="errors.number" class="text-red-500">{{ errors.number }}</small>
    </div>

    <div class="flex flex-col gap-1">
      <label class="text-sm font-medium text-surface-700">Product</label>
      <ProductSelect
        :model-value="productId"
        :disabled="mode === 'edit'"
        @update:model-value="productId = $event ?? ''"
      />
      <small v-if="mode === 'edit'" class="text-surface-400">
        Product cannot be changed.
      </small>
      <small v-if="errors.productId" class="text-red-500">{{ errors.productId }}</small>
    </div>

    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <div class="flex flex-col gap-1">
        <label for="mfg" class="text-sm font-medium text-surface-700">
          Manufacture Date
        </label>
        <DatePicker
          input-id="mfg"
          v-model="manufactureDate"
          date-format="yy-mm-dd"
          show-icon
          show-button-bar
          fluid
        />
      </div>
      <div class="flex flex-col gap-1">
        <label for="exp" class="text-sm font-medium text-surface-700">
          Expiration Date
        </label>
        <DatePicker
          input-id="exp"
          v-model="expirationDate"
          date-format="yy-mm-dd"
          show-icon
          show-button-bar
          fluid
          :invalid="!!errors.expirationDate"
        />
        <small v-if="errors.expirationDate" class="text-red-500">
          {{ errors.expirationDate }}
        </small>
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
