<script setup lang="ts">
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import { useSeedData, useTruncateData } from './useDatabase'
import { serverErrorText } from './server-error'

const confirm = useConfirm()
const toast = useToast()

const seedData = useSeedData()
const truncateData = useTruncateData()

function confirmSeed() {
  confirm.require({
    message:
      'Populate the warehouse with demo locations, products, lots and inventory? ' +
      'This only runs when the warehouse is empty.',
    header: 'Seed data',
    icon: 'pi pi-database',
    rejectProps: { label: 'Cancel', severity: 'secondary', text: true },
    acceptProps: { label: 'Seed data' },
    accept: () => {
      seedData.mutate(undefined, {
        onSuccess: () => {
          toast.add({
            severity: 'success',
            summary: 'Seed complete',
            detail: 'The warehouse has been seeded with demo data.',
            life: 3000,
          })
        },
        onError: (err) => {
          toast.add({
            severity: 'error',
            summary: 'Seed failed',
            detail: serverErrorText(err),
            life: 5000,
          })
        },
      })
    },
  })
}

function confirmTruncate() {
  confirm.require({
    message:
      'Permanently delete all warehouse data — locations, products, lots, inventory ' +
      'and stock movements? User accounts are not affected. This cannot be undone.',
    header: 'Truncate data',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', text: true },
    acceptProps: { label: 'Truncate', severity: 'danger' },
    accept: () => {
      truncateData.mutate(undefined, {
        onSuccess: () => {
          toast.add({
            severity: 'success',
            summary: 'Data truncated',
            detail: 'All warehouse data has been deleted.',
            life: 3000,
          })
        },
        onError: (err) => {
          toast.add({
            severity: 'error',
            summary: 'Truncate failed',
            detail: serverErrorText(err),
            life: 5000,
          })
        },
      })
    },
  })
}

const busy = () => seedData.isPending.value || truncateData.isPending.value
</script>

<template>
  <div class="flex flex-col gap-4">
    <p class="text-sm text-surface-500">
      Maintenance actions for the warehouse data. These affect every record but never
      touch user accounts.
    </p>

    <div
      class="flex items-center justify-between gap-4 p-4 rounded-lg border border-surface-200"
    >
      <div>
        <h2 class="font-medium text-surface-900">Seed data</h2>
        <p class="text-sm text-surface-500 mt-1">
          Populate the warehouse with demo locations, products, lots and inventory.
          Only runs when the warehouse is empty.
        </p>
      </div>
      <Button
        label="Seed data"
        icon="pi pi-database"
        class="shrink-0"
        :loading="seedData.isPending.value"
        :disabled="busy()"
        @click="confirmSeed"
      />
    </div>

    <div
      class="flex items-center justify-between gap-4 p-4 rounded-lg border border-red-200"
    >
      <div>
        <h2 class="font-medium text-surface-900">Truncate data</h2>
        <p class="text-sm text-surface-500 mt-1">
          Permanently delete all warehouse data. User accounts are not affected.
        </p>
      </div>
      <Button
        label="Truncate data"
        icon="pi pi-trash"
        severity="danger"
        class="shrink-0"
        :loading="truncateData.isPending.value"
        :disabled="busy()"
        @click="confirmTruncate"
      />
    </div>
  </div>
</template>
