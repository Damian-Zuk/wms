export interface InventorySummary {
  onHandUnits: number
  reservedUnits: number
  availableUnits: number
  distinctSkus: number
  occupiedLocations: number
  activeLocations: number
}

export interface FlowToday {
  receivedToday: number
  shippedToday: number
  receivedPrevDay: number
  shippedPrevDay: number
}

export interface WorkOrders {
  draftStockIns: number
  putawayStockIns: number
  draftStockOuts: number
  pickingStockOuts: number
}

export interface DashboardAlerts {
  expiringSoonLots: number
  expiredOnHandLots: number
  blockedLocations: number
  inactiveLocations: number
}

/** One day of throughput. `date` is an API DateOnly string "yyyy-MM-dd". */
export interface ThroughputPoint {
  date: string
  received: number
  shipped: number
}

export interface DashboardOverviewDto {
  inventory: InventorySummary
  flowToday: FlowToday
  workOrders: WorkOrders
  alerts: DashboardAlerts
  throughput: ThroughputPoint[]
}

/** A slice of a strategy-usage breakdown. `strategy` is the enum name. */
export interface StrategySlice {
  strategy: string
  units: number
}

/** One day of a single-metric series. `date` is an API DateOnly string "yyyy-MM-dd". */
export interface DailyUnits {
  date: string
  units: number
}

export interface TopProduct {
  productId: string
  sku: string
  name: string
  units: number
}

// --- Inbound ---

export interface StockInStatusCounts {
  draft: number
  putaway: number
  completed: number
  cancelled: number
}

export interface PutawayProgress {
  plannedUnits: number
  placedUnits: number
  completionPercent: number
}

export interface InboundOverviewDto {
  statusCounts: StockInStatusCounts
  putawayProgress: PutawayProgress
  manualOverridePercent: number
  avgReceivingHours: number | null
  putawayStrategyMix: StrategySlice[]
  receivedSeries: DailyUnits[]
}

// --- Outbound ---

export interface StockOutStatusCounts {
  draft: number
  picking: number
  completed: number
  cancelled: number
}

export interface PickProgress {
  plannedUnits: number
  pickedUnits: number
  completionPercent: number
}

export interface Cancellations {
  fromDraft: number
  fromPicking: number
}

export interface OutboundOverviewDto {
  statusCounts: StockOutStatusCounts
  pickProgress: PickProgress
  avgFulfillmentHours: number | null
  cancellations: Cancellations
  pickingStrategyMix: StrategySlice[]
  topPickedProducts: TopProduct[]
  shippedSeries: DailyUnits[]
}
