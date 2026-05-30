import type { LocationType, StockInStatus, TemperatureZone } from '@/types/enums'

export type TagSeverity =
  | 'success'
  | 'info'
  | 'warn'
  | 'secondary'
  | 'danger'
  | 'contrast'

export const temperatureZoneSeverity: Record<TemperatureZone, TagSeverity> = {
  Ambient: 'success',
  Chilled: 'info',
  Frozen: 'contrast',
}

export const locationTypeSeverity: Record<LocationType, TagSeverity> = {
  Storage: 'info',
  Quarantine: 'warn',
  Returns: 'secondary',
}

export const stockInStatusSeverity: Record<StockInStatus, TagSeverity> = {
  Draft: 'secondary',
  Receiving: 'info',
  Received: 'warn',
  Completed: 'success',
  Cancelled: 'danger',
}
