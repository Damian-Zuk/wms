import { computed, type Ref } from 'vue'
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/vue-query'
import { handlingUnitsApi } from '@/api/endpoints/handling-units'
import { qk } from '@/api/query-keys'
import type {
  CreateHandlingUnitCommand,
  HandlingUnitFilters,
  MoveHandlingUnitRequest,
  PackHandlingUnitRequest,
} from '@/types/handling-units'

export function useHandlingUnits(filters: Ref<HandlingUnitFilters>) {
  return useQuery({
    queryKey: computed(() => qk.handlingUnits.list(filters.value)),
    queryFn: () => handlingUnitsApi.list(filters.value),
    placeholderData: keepPreviousData,
    staleTime: 60_000,
  })
}

export function useHandlingUnit(id: Ref<string>) {
  return useQuery({
    queryKey: computed(() => qk.handlingUnits.detail(id.value)),
    queryFn: () => handlingUnitsApi.get(id.value),
    enabled: computed(() => !!id.value),
  })
}

/**
 * Handling units standing at a location, as select options. Disabled (and
 * empty) until a location is chosen; keyed by locationId so each scope caches
 * separately.
 */
export function useHandlingUnitOptions(locationId: Ref<string | undefined>) {
  const query = useQuery({
    queryKey: computed(() => qk.handlingUnits.options(locationId.value)),
    queryFn: () =>
      handlingUnitsApi.list({ locationId: locationId.value, page: 1, pageSize: 200 }),
    enabled: computed(() => !!locationId.value),
    staleTime: 60_000,
  })

  const options = computed(() =>
    (query.data.value?.items ?? []).map((h) => ({ label: h.code, value: h.id })),
  )

  return { ...query, options }
}

function invalidateStock(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: qk.handlingUnits.all })
  void qc.invalidateQueries({ queryKey: qk.inventories.all })
}

export function useCreateHandlingUnit() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateHandlingUnitCommand) => handlingUnitsApi.create(body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.handlingUnits.all })
    },
  })
}

export function useMoveHandlingUnit(id: Ref<string>) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: MoveHandlingUnitRequest) => handlingUnitsApi.move(id.value, body),
    onSuccess: () => {
      invalidateStock(qc)
      void qc.invalidateQueries({ queryKey: qk.stockMovements.all })
    },
  })
}

export function usePackHandlingUnit(id: Ref<string>) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: PackHandlingUnitRequest) => handlingUnitsApi.pack(id.value, body),
    onSuccess: () => invalidateStock(qc),
  })
}

export function useUnpackHandlingUnit(id: Ref<string>) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: PackHandlingUnitRequest) =>
      handlingUnitsApi.unpack(id.value, body),
    onSuccess: () => invalidateStock(qc),
  })
}

export function useDeleteHandlingUnit() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => handlingUnitsApi.remove(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.handlingUnits.all })
    },
  })
}
