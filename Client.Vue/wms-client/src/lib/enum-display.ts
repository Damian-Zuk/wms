import type {
  LocationType,
  PutawayStrategyType,
  StockInStatus,
  TemperatureZone,
} from '@/types/enums'

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

export const putawayStrategyLabel: Record<PutawayStrategyType, string> = {
  FixedLocation: 'Fixed location',
  ConsolidateSameSku: 'Consolidate SKU',
  NearestEmpty: 'Nearest empty',
  Manual: 'Manual',
}

export const putawayStrategySeverity: Record<PutawayStrategyType, TagSeverity> = {
  FixedLocation: 'info',
  ConsolidateSameSku: 'warn',
  NearestEmpty: 'secondary',
  Manual: 'contrast',
}
