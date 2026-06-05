import { computed, type Ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { dashboardApi } from '@/api/endpoints/dashboard'
import { qk } from '@/api/query-keys'

export function useDashboardOverview(days: Ref<number>) {
  return useQuery({
    queryKey: computed(() => qk.dashboard.overview(days.value)),
    queryFn: () => dashboardApi.overview(days.value),
  })
}

export function useInboundOverview(days: Ref<number>) {
  return useQuery({
    queryKey: computed(() => qk.dashboard.inbound(days.value)),
    queryFn: () => dashboardApi.inbound(days.value),
  })
}

export function useOutboundOverview(days: Ref<number>) {
  return useQuery({
    queryKey: computed(() => qk.dashboard.outbound(days.value)),
    queryFn: () => dashboardApi.outbound(days.value),
  })
}
