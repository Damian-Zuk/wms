<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import Button from 'primevue/button'
import Select from 'primevue/select'
import DataTableWrapper from '@/components/common/DataTableWrapper.vue'
import ListingHeader from '@/components/common/ListingHeader.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import LocationSelect from '@/components/pickers/LocationSelect.vue'
import CreateHandlingUnitDialog from './CreateHandlingUnitDialog.vue'
import { useHandlingUnits } from './useHandlingUnits'
import { handlingUnitTypeLabel, handlingUnitTypeSeverity } from '@/lib/enum-display'
import { useAuthStore } from '@/stores/auth'
import { sortOrderOf, toSortFilters, type SortChange } from '@/lib/sort'
import type { HandlingUnitType } from '@/types/enums'
import type { HandlingUnitFilters, HandlingUnitListItemDto } from '@/types/handling-units'

const router = useRouter()
const auth = useAuthStore()

const filters = ref<HandlingUnitFilters>({ search: '', page: 1, pageSize: 20 })
const search = ref('')
const locationFilter = ref<string | null>(null)
const typeFilter = ref<HandlingUnitType | null>(null)
const contentsFilter = ref<'all' | 'withStock' | 'empty'>('all')
const createVisible = ref(false)

const typeOptions = computed(() =>
  (Object.keys(handlingUnitTypeLabel) as HandlingUnitType[]).map((t) => ({
    label: handlingUnitTypeLabel[t],
    value: t,
  })),
)

const contentsOptions = [
  { label: 'All units', value: 'all' },
  { label: 'With stock', value: 'withStock' },
  { label: 'Empty', value: 'empty' },
]

const { data, isFetching, refetch } = useHandlingUnits(filters)

function applySearch() {
  filters.value = { ...filters.value, search: search.value.trim(), page: 1 }
}

function onLocationChange(value: string | null) {
  filters.value = { ...filters.value, locationId: value ?? undefined, page: 1 }
}

function onTypeChange(value: HandlingUnitType | null) {
  filters.value = { ...filters.value, type: value ?? undefined, page: 1 }
}

function onContentsChange(value: 'all' | 'withStock' | 'empty') {
  filters.value = {
    ...filters.value,
    isEmpty: value === 'all' ? undefined : value === 'empty',
    page: 1,
  }
}

function setPage(page: number) {
  filters.value = { ...filters.value, page }
}

function setPageSize(pageSize: number) {
  filters.value = { ...filters.value, pageSize, page: 1 }
}

function onSort(change: SortChange) {
  filters.value = { ...filters.value, ...toSortFilters(change), page: 1 }
}

function openUnit(unit: HandlingUnitListItemDto) {
  router.push({ name: 'handling-unit-detail', params: { id: unit.id } })
}
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1600px">
    <div class="flex items-center justify-between gap-4">
      <ListingHeader
        title="Handling Units"
        :count="data?.totalCount ?? 0"
        :loading="isFetching"
        @refresh="refetch"
      />

      <div class="flex items-center gap-2">
        <div class="w-40">
          <Select
            v-model="contentsFilter"
            :options="contentsOptions"
            option-label="label"
            option-value="value"
            fluid
            @update:model-value="onContentsChange"
          />
        </div>
        <div class="w-40">
          <Select
            v-model="typeFilter"
            :options="typeOptions"
            option-label="label"
            option-value="value"
            show-clear
            placeholder="All types"
            fluid
            @update:model-value="onTypeChange"
          />
        </div>
        <div class="w-56">
          <LocationSelect
            v-model="locationFilter"
            show-clear
            placeholder="All locations"
            @update:model-value="onLocationChange"
          />
        </div>
        <IconField>
          <InputIcon class="pi pi-search" />
          <InputText v-model="search" placeholder="Search code" @keyup.enter="applySearch" />
        </IconField>
        <Button
          v-if="auth.canMutate"
          label="New Handling Unit"
          icon="pi pi-plus"
          @click="createVisible = true"
        />
      </div>
    </div>

    <DataTableWrapper
      :items="data?.items ?? []"
      :total-count="data?.totalCount ?? 0"
      :page="filters.page"
      :page-size="filters.pageSize"
      :loading="isFetching"
      :sort-field="filters.sortBy ?? null"
      :sort-order="sortOrderOf(filters)"
      class="cursor-pointer"
      @update:page="setPage"
      @update:page-size="setPageSize"
      @update:sort="onSort"
      @row-click="openUnit"
    >
      <Column field="code" header="Code" sortable sort-field="code" style="width: 14rem" />
      <Column header="Type" sortable sort-field="type" style="width: 10rem">
        <template #body="{ data: row }: { data: HandlingUnitListItemDto }">
          <StatusBadge
            :value="handlingUnitTypeLabel[row.type]"
            :severity="handlingUnitTypeSeverity[row.type]"
          />
        </template>
      </Column>
      <Column header="Location" sortable sort-field="location">
        <template #body="{ data: row }: { data: HandlingUnitListItemDto }">
          <span v-if="row.location">{{ row.location.code }} · {{ row.location.address }}</span>
          <StatusBadge v-else value="Expected" severity="secondary" />
        </template>
      </Column>
      <Column header="On Hand" sortable sort-field="onhand" style="width: 9rem">
        <template #body="{ data: row }: { data: HandlingUnitListItemDto }">
          <span :class="row.totalOnHand === 0 ? 'text-surface-400' : 'text-surface-700'">
            {{ row.totalOnHand }}
          </span>
        </template>
      </Column>
      <Column header="Products" style="width: 9rem">
        <template #body="{ data: row }: { data: HandlingUnitListItemDto }">
          {{ row.productCount }}
        </template>
      </Column>
    </DataTableWrapper>

    <CreateHandlingUnitDialog v-model:visible="createVisible" />
  </section>
</template>
