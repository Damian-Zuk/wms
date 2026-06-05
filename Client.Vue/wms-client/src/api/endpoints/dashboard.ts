import { http } from '../http'
import type {
  DashboardOverviewDto,
  InboundOverviewDto,
  OutboundOverviewDto,
} from '@/types/dashboard'

export const dashboardApi = {
  overview: (days: number) =>
    http
      .get<DashboardOverviewDto>('/dashboard/overview', { params: { days } })
      .then((r) => r.data),

  inbound: (days: number) =>
    http
      .get<InboundOverviewDto>('/dashboard/inbound', { params: { days } })
      .then((r) => r.data),

  outbound: (days: number) =>
    http
      .get<OutboundOverviewDto>('/dashboard/outbound', { params: { days } })
      .then((r) => r.data),
}
