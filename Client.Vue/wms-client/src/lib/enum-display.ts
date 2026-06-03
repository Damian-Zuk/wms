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
  PreferredLocation: 'Preferred location',
  ConsolidateSameSku: 'Consolidate SKU',
  ConsolidateSameLot: 'Consolidate lot',
  NearestEmpty: 'Nearest empty',
  Proximity: 'Proximity',
  Manual: 'Manual',
}

export const putawayStrategySeverity: Record<PutawayStrategyType, TagSeverity> = {
  PreferredLocation: 'info',
  ConsolidateSameSku: 'warn',
  ConsolidateSameLot: 'secondary',
  NearestEmpty: 'secondary',
  Proximity: 'info',
  Manual: 'contrast',
}
