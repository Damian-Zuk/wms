import { computed, type Ref } from 'vue'
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/vue-query'
import { locationsApi } from '@/api/endpoints/locations'
import { qk } from '@/api/query-keys'
import type {
  CreateLocationCommand,
  LocationFilters,
  UpdateLocationRequest,
} from '@/types/locations'

export function useLocations(filters: Ref<LocationFilters>) {
  return useQuery({
    queryKey: computed(() => qk.locations.list(filters.value)),
    queryFn: () => locationsApi.list(filters.value),
    placeholderData: keepPreviousData,
    staleTime: 5 * 60_000,
  })
}

/**
 * Loads locations for use as select options / id→location lookups.
 * Shared key so all callers (pickers, reference columns) dedupe to one fetch.
 */
export function useLocationOptions() {
  const query = useQuery({
    queryKey: qk.locations.options(),
    queryFn: () => locationsApi.list({ page: 1, pageSize: 200 }),
    staleTime: 10 * 60_000,
  })

  const options = computed(() =>
    (query.data.value?.items ?? []).map((l) => ({
      label: `${l.code} — ${l.display}`,
      value: l.id,
    })),
  )

  const byId = computed(
    () => new Map((query.data.value?.items ?? []).map((l) => [l.id, l])),
  )

  return { ...query, options, byId }
}

export function useLocation(id: Ref<string>) {
  return useQuery({
    queryKey: computed(() => qk.locations.detail(id.value)),
    queryFn: () => locationsApi.get(id.value),
    enabled: computed(() => !!id.value),
  })
}

export function useCreateLocation() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateLocationCommand) => locationsApi.create(body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.locations.all })
    },
  })
}

export function useUpdateLocation(id: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateLocationRequest) => locationsApi.update(id, body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.locations.all })
      void qc.invalidateQueries({ queryKey: qk.locations.detail(id) })
    },
  })
}

export function useSetLocationPreferredProducts(id: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (productIds: string[]) =>
      locationsApi.setPreferredProducts(id, productIds),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.products.all })
      void qc.invalidateQueries({ queryKey: qk.locations.detail(id) })
    },
  })
}

export function useDeleteLocation() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => locationsApi.remove(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.locations.all })
    },
  })
}
