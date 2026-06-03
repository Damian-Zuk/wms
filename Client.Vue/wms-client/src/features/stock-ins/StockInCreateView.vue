<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import Message from 'primevue/message'
import PageHeader from '@/components/common/PageHeader.vue'
import ProductSelect from '@/components/pickers/ProductSelect.vue'
import LotSelect from '@/components/pickers/LotSelect.vue'
import { useCreateStockIn } from './useStockIns'
import type { CreateStockInCommand } from '@/types/stock-ins'

interface Row {
  productId: string
  lotId: string | null
  quantity: number | null
}

const router = useRouter()
const toast = useToast()
const create = useCreateStockIn()

const submitting = computed(() => create.isPending.value)
const rows = reactive<Row[]>([{ productId: '', lotId: null, quantity: 1 }])
const rowErrors = ref<Record<number, string>>({})
const serverError = ref<string | null>(null)

function addRow() {
  rows.push({ productId: '', lotId: null, quantity: 1 })
}

function removeRow(index: number) {
  rows.splice(index, 1)
}

function onProductChange(row: Row, value: string | null) {
  row.productId = value ?? ''
  row.lotId = null // a lot is product-specific; reset when the product changes
}

function validate(): boolean {
  rowErrors.value = {}
  let ok = true
  rows.forEach((row, i) => {
    if (!row.productId || !row.quantity || row.quantity <= 0) {
      rowErrors.value[i] = 'Product and a quantity greater than 0 are required.'
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
    })),
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
  <section class="p-6 flex flex-col gap-6" style="max-width: 760px">
    <PageHeader
      title="New Stock-In"
      subtitle="Add the lines to receive — the system plans where to put them"
    />

    <Message v-if="serverError" severity="error" :closable="false">{{ serverError }}</Message>

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
