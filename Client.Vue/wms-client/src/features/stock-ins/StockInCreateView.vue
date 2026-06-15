<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import Button from 'primevue/button'
import Message from 'primevue/message'
import PageHeader from '@/components/common/PageHeader.vue'
import ProductSelect from '@/components/pickers/ProductSelect.vue'
import LotSelect from '@/components/pickers/LotSelect.vue'
import { useCreateStockIn } from './useStockIns'
import { handlingUnitTypeLabel } from '@/lib/enum-display'
import type { HandlingUnitType } from '@/types/enums'
import type { CreateStockInCommand } from '@/types/stock-ins'

interface HuRow {
  quantity: number | null
  type: HandlingUnitType
  code: string
}

interface Row {
  productId: string
  lotId: string | null
  quantity: number | null
  handlingUnits: HuRow[]
}

const router = useRouter()
const toast = useToast()
const create = useCreateStockIn()

const submitting = computed(() => create.isPending.value)
const description = ref<string>('')
const rows = reactive<Row[]>([{ productId: '', lotId: null, quantity: 1, handlingUnits: [] }])
const rowErrors = ref<Record<number, string>>({})
const serverError = ref<string | null>(null)

const typeOptions = computed(() =>
  (Object.keys(handlingUnitTypeLabel) as HandlingUnitType[]).map((t) => ({
    label: handlingUnitTypeLabel[t],
    value: t,
  })),
)

function addRow() {
  rows.push({ productId: '', lotId: null, quantity: 1, handlingUnits: [] })
}

function removeRow(index: number) {
  rows.splice(index, 1)
}

function onProductChange(row: Row, value: string | null) {
  row.productId = value ?? ''
  row.lotId = null // a lot is product-specific; reset when the product changes
}

function addHandlingUnit(row: Row) {
  row.handlingUnits.push({ quantity: null, type: 'Pallet', code: '' })
}

function removeHandlingUnit(row: Row, index: number) {
  row.handlingUnits.splice(index, 1)
}

function declaredTotal(row: Row) {
  return row.handlingUnits.reduce((sum, h) => sum + (h.quantity ?? 0), 0)
}

function looseRemainder(row: Row) {
  return (row.quantity ?? 0) - declaredTotal(row)
}

function validate(): boolean {
  rowErrors.value = {}
  let ok = true
  rows.forEach((row, i) => {
    if (!row.productId || !row.quantity || row.quantity <= 0) {
      rowErrors.value[i] = 'Product and a quantity greater than 0 are required.'
      ok = false
      return
    }
    if (row.handlingUnits.some((h) => !h.quantity || h.quantity <= 0)) {
      rowErrors.value[i] = 'Every handling unit needs a quantity greater than 0.'
      ok = false
      return
    }
    if (declaredTotal(row) > row.quantity) {
      rowErrors.value[i] = 'Declared handling units exceed the line quantity.'
      ok = false
    }
  })
  return ok
}

function onSubmit() {
  serverError.value = null
  if (!validate()) return

  const body: CreateStockInCommand = {
    lines: rows.map((row) => ({
      productId: row.productId,
      lotId: row.lotId,
      quantity: row.quantity as number,
      handlingUnits: row.handlingUnits.length
        ? row.handlingUnits.map((h) => ({
            quantity: h.quantity as number,
            type: h.type,
            code: h.code.trim() || null,
          }))
        : undefined,
    })),
    description: description.value.trim() || null,
  }

  create.mutate(body, {
    onSuccess: (newId) => {
      toast.add({ severity: 'success', summary: 'Stock-in created', life: 3000 })
      router.push({ name: 'stock-in-detail', params: { id: newId } })
    },
    onError: (err) => {
      serverError.value = err.message
    },
  })
}
</script>

<template>
  <section class="p-6 flex flex-col gap-6" style="max-width: 820px">
    <PageHeader
      title="New Stock-In"
      subtitle="Add the lines to receive — the system plans where to put them"
    />

    <Message v-if="serverError" severity="error" :closable="false">{{ serverError }}</Message>

    <div class="flex flex-col gap-1">
      <label class="text-xs text-surface-500">Description (optional)</label>
      <Textarea v-model="description" :maxlength="500" rows="2" autoResize fluid placeholder="Add a note…" />
    </div>

    <div class="flex flex-col gap-3">
      <div
        v-for="(row, i) in rows"
        :key="i"
        class="rounded-lg border border-surface-200 bg-white p-4 flex flex-col gap-3"
      >
        <div class="flex items-center justify-between">
          <span class="text-sm font-medium text-surface-700">Line {{ i + 1 }}</span>
          <Button
            icon="pi pi-trash"
            severity="danger"
            text
            :disabled="rows.length === 1"
            aria-label="Remove line"
            @click="removeRow(i)"
          />
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-[1fr_1fr_8rem] gap-3">
          <div class="flex flex-col gap-1">
            <label class="text-xs text-surface-500">Product</label>
            <ProductSelect
              :model-value="row.productId"
              @update:model-value="(v) => onProductChange(row, v)"
            />
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs text-surface-500">Lot (optional)</label>
            <LotSelect v-model="row.lotId" :product-id="row.productId || undefined" />
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs text-surface-500">Quantity</label>
            <InputNumber v-model="row.quantity" :min="1" :use-grouping="false" fluid />
          </div>
        </div>

        <!-- ASN-style handling unit declarations: each lands whole in one location. -->
        <div class="rounded-lg bg-surface-50 p-3 flex flex-col gap-2">
          <div class="flex items-center justify-between">
            <span class="text-xs font-medium text-surface-600">
              <i class="pi pi-inbox mr-1" />Handling units (optional)
            </span>
            <span
              v-if="row.handlingUnits.length"
              class="text-xs"
              :class="looseRemainder(row) < 0 ? 'text-red-600' : 'text-surface-500'"
            >
              Declared {{ declaredTotal(row) }} / {{ row.quantity ?? 0 }}
              <template v-if="looseRemainder(row) > 0"> · {{ looseRemainder(row) }} loose</template>
            </span>
          </div>

          <div
            v-for="(hu, j) in row.handlingUnits"
            :key="j"
            class="grid grid-cols-[10rem_7rem_1fr_auto] gap-2 items-center"
          >
            <Select
              v-model="hu.type"
              :options="typeOptions"
              option-label="label"
              option-value="value"
              size="small"
              fluid
            />
            <InputNumber
              v-model="hu.quantity"
              :min="1"
              :use-grouping="false"
              placeholder="Qty"
              size="small"
              fluid
            />
            <InputText
              v-model="hu.code"
              placeholder="Code (generated when empty)"
              maxlength="50"
              size="small"
              fluid
            />
            <Button
              icon="pi pi-times"
              severity="danger"
              text
              size="small"
              aria-label="Remove handling unit"
              @click="removeHandlingUnit(row, j)"
            />
          </div>

          <div>
            <Button
              label="Add handling unit"
              icon="pi pi-plus"
              severity="secondary"
              text
              size="small"
              @click="addHandlingUnit(row)"
            />
          </div>
        </div>

        <small v-if="rowErrors[i]" class="text-red-500">{{ rowErrors[i] }}</small>
      </div>

      <div>
        <Button label="Add line" icon="pi pi-plus" severity="secondary" outlined @click="addRow" />
      </div>
    </div>

    <div class="flex gap-2 justify-end pt-2">
      <Button
        type="button"
        label="Cancel"
        severity="secondary"
        text
        @click="router.push({ name: 'stock-ins' })"
      />
      <Button label="Create" icon="pi pi-check" :loading="submitting" @click="onSubmit" />
    </div>
  </section>
</template>
