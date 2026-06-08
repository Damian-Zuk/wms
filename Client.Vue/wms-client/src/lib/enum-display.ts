import type {
  LocationType,
  PickingStrategyType,
  PutawayStrategyType,
  StockInStatus,
  StockMovementSource,
  StockMovementType,
  StockOutStatus,
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
  Chilled: 'secondary',
  Frozen: 'info',
}

export const locationTypeSeverity: Record<LocationType, TagSeverity> = {
  Storage: 'info',
  Quarantine: 'warn',
  Returns: 'secondary',
}

export const stockInStatusSeverity: Record<StockInStatus, TagSeverity> = {
  Draft: 'secondary',
  Putaway: 'info',
  Completed: 'success',
  Cancelled: 'danger',
}

export const putawayStrategyLabel: Record<PutawayStrategyType, string> = {
  PreferredLocation: 'Preferred Location',
  ConsolidateSameSku: 'Consolidate SKU',
  ConsolidateSameLot: 'Consolidate Lot',
  NearestEmpty: 'Nearest Empty',
  NearestAvailable: 'Nearest Available',
  Proximity: 'Proximity',
  Manual: 'Manual',
}

export const putawayStrategySeverity: Record<PutawayStrategyType, TagSeverity> = {
  PreferredLocation: 'info',
  ConsolidateSameSku: 'warn',
  ConsolidateSameLot: 'secondary',
  NearestEmpty: 'secondary',
  NearestAvailable: 'warn',
  Proximity: 'info',
  Manual: 'contrast',
}

export const stockOutStatusSeverity: Record<StockOutStatus, TagSeverity> = {
  Draft: 'secondary',
  Picking: 'info',
  Completed: 'success',
  Cancelled: 'danger',
}

export const pickingStrategyLabel: Record<PickingStrategyType, string> = {
  Fefo: 'FEFO (earliest expiry)',
  Fifo: 'FIFO (oldest first)',
}

export const pickingStrategySeverity: Record<PickingStrategyType, TagSeverity> = {
  Fefo: 'info',
  Fifo: 'warn',
}

export const stockMovementTypeSeverity: Record<StockMovementType, TagSeverity> = {
  In: 'success',
  Out: 'danger',
}

export const stockMovementSourceLabel: Record<StockMovementSource, string> = {
  StockIn: 'Stock-In',
  StockOut: 'Stock-Out',
  Adjustment: 'Adjustment',
  Transfer: 'Transfer',
  StockOutCancellation: 'Stock-Out Cancellation',
  StockInCancellation: 'Stock-In Cancellation',
}

export const stockMovementSourceSeverity: Record<StockMovementSource, TagSeverity> = {
  StockIn: 'success',
  StockOut: 'info',
  Adjustment: 'warn',
  Transfer: 'secondary',
  StockOutCancellation: 'contrast',
  StockInCancellation: 'danger',
}
