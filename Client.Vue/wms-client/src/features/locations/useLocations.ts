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

export function useDeleteLocation() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => locationsApi.remove(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.locations.all })
    },
  })
}
