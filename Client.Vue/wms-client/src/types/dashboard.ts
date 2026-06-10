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

// --- Inventory ---

export interface InventoryStockSummary {
  onHandUnits: number
  reservedUnits: number
  availableUnits: number
  distinctSkus: number
  totalWeightKg: number
  totalVolume: number
  /** On-hand stock valued at unit cost, in the warehouse's base currency. */
  totalValue: number
}

/** A slice of an on-hand composition breakdown. `key` is the enum name. */
export interface CompositionSlice {
  key: string
  units: number
}

export interface ExpiryBuckets {
  expired: number
  within7: number
  within30: number
  within60: number
  within90: number
  beyond90: number
  noExpiry: number
}

/** On-hand value (units × unit cost) bucketed by days-to-expiry. */
export interface ExpiryValueBuckets {
  expired: number
  within7: number
  within30: number
  within60: number
  within90: number
  beyond90: number
  noExpiry: number
}

export interface InventoryOverviewDto {
  summary: InventoryStockSummary
  byTemperatureZone: CompositionSlice[]
  byLocationType: CompositionSlice[]
  topProducts: TopProduct[]
  expiryBuckets: ExpiryBuckets
  expiryValueBuckets: ExpiryValueBuckets
}

// --- Capacity ---

export interface LocationsSummary {
  total: number
  active: number
  inactive: number
  blocked: number
  occupied: number
  empty: number
}

export interface CapacityUtilization {
  unitsPercent: number
  weightPercent: number
  volumePercent: number
  usedUnits: number
  unitsCapacity: number
  usedWeight: number
  weightCapacity: number
  usedVolume: number
  volumeCapacity: number
}

export interface LocationFill {
  locationId: string
  code: string
  address: string
  fillPercent: number
  onHandUnits: number
  isBlocked: boolean
}

export interface ZoneFill {
  zone: string
  fillPercent: number
  locationCount: number
}

export interface BlockedLocation {
  locationId: string
  code: string
  address: string
  reason: string | null
}

export interface CapacityOverviewDto {
  summary: LocationsSummary
  utilization: CapacityUtilization
  fullestLocations: LocationFill[]
  byZone: ZoneFill[]
  blockedLocations: BlockedLocation[]
}
