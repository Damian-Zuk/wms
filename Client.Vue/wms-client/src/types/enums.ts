export type TemperatureZone = 'Ambient' | 'Chilled' | 'Frozen'

export type LocationType = 'Storage' | 'Quarantine' | 'Returns'

export type StockInStatus = 'Draft' | 'Receiving' | 'Received' | 'Completed' | 'Cancelled'

export type StockOutStatus =
  | 'Draft'
  | 'Picking'
  | 'Packed'
  | 'Shipped'
  | 'Completed'
  | 'Cancelled'

export type PutawayStrategyType =
  | 'PreferredLocation'
  | 'ConsolidateSameSku'
  | 'ConsolidateSameLot'
  | 'NearestEmpty'
  | 'Manual'

export type StockMovementType = 'In' | 'Out'

export type StockMovementSource =
  | 'StockIn'
  | 'StockOut'
  | 'Adjustment'
  | 'Transfer'
  | 'StockOutCancellation'
