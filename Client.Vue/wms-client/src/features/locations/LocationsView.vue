<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import Select from 'primevue/select'
import Button from 'primevue/button'
import DataTableWrapper from '@/components/common/DataTableWrapper.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import CapacityBar from './CapacityBar.vue'
import { useLocations } from './useLocations'
import { useAuthStore } from '@/stores/auth'
import { locationTypeSeverity, temperatureZoneSeverity } from '@/lib/enum-display'
import type { LocationDto, LocationFilters } from '@/types/locations'
import type { LocationType } from '@/types/enums'

const router = useRouter()
const auth = useAuthStore()

const filters = ref<LocationFilters>({ search: '', page: 1, pageSize: 20 })
const search = ref('')

const typeOptions: { label: string; value: LocationType }[] = [
  { label: 'Storage', value: 'Storage' },
  { label: 'Quarantine', value: 'Quarantine' },
  { label: 'Returns', value: 'Returns' },
]

const { data, isFetching } = useLocations(filters)

function applySearch() {
  filters.value = { ...filters.value, search: search.value.trim(), page: 1 }
}

function onTypeChange(value: LocationType | null) {
  filters.value = { ...filters.value, type: value ?? undefined, page: 1 }
}

function setPage(page: number) {
  filters.value = { ...filters.value, page }
}

function setPageSize(pageSize: number) {
  filters.value = { ...filters.value, pageSize, page: 1 }
}

function openLocation(location: LocationDto) {
  router.push({ name: 'location-detail', params: { id: location.id } })
}
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1600px">
    <div class="flex items-center justify-between gap-4">
      <h1 class="text-2xl font-semibold text-surface-900">Locations</h1>

      <div class="flex items-center gap-2">
        <Select
          :options="typeOptions"
          option-label="label"
          option-value="value"
          placeholder="All types"
          show-clear
          @update:model-value="onTypeChange"
        />
        <IconField>
          <InputIcon class="pi pi-search" />
          <InputText
            v-model="search"
            placeholder="Search code or description"
            @keyup.enter="applySearch"
          />
        </IconField>
        <Button
          v-if="auth.canMutate"
          label="New Location"
          icon="pi pi-plus"
          @click="router.push({ name: 'location-create' })"
        />
      </div>
    </div>

    <DataTableWrapper
      :items="data?.items ?? []"
      :total-count="data?.totalCount ?? 0"
      :page="filters.page"
      :page-size="filters.pageSize"
      :loading="isFetching"
      class="cursor-pointer"
      @update:page="setPage"
      @update:page-size="setPageSize"
      @row-click="openLocation"
    >
      <Column field="code" header="Code" style="width: 12rem" />
      <Column field="display" header="Address" />
      <Column header="Type" style="width: 10rem">
        <template #body="{ data: row }: { data: LocationDto }">
          <StatusBadge :value="row.type" :severity="locationTypeSeverity[row.type]" />
        </template>
      </Column>
      <Column header="Temp. Zone" style="width: 10rem">
        <template #body="{ data: row }: { data: LocationDto }">
          <StatusBadge
            :value="row.temperatureZone"
            :severity="temperatureZoneSeverity[row.temperatureZone]"
          />
        </template>
      </Column>
      <Column header="Status" style="width: 9rem">
        <template #body="{ data: row }: { data: LocationDto }">
          <StatusBadge v-if="row.isBlocked" value="Blocked" severity="danger" />
          <StatusBadge v-else-if="row.isActive" value="Active" severity="success" />
          <StatusBadge v-else value="Inactive" severity="secondary" />
        </template>
      </Column>
      <Column header="Capacity" style="width: 12rem">
        <template #body="{ data: row }: { data: LocationDto }">
          <CapacityBar :location="row" />
        </template>
      </Column>
    </DataTableWrapper>
  </section>
</template>
