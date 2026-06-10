// Single source of truth for how monetary amounts (product unit cost, inventory
// value) are displayed. Unit prices are stored as a per-unit cost in the
// warehouse's base currency. Change CURRENCY_CODE here to re-denominate the UI.
const CURRENCY_CODE = 'USD'

const currencyFormatter = new Intl.NumberFormat(undefined, {
  style: 'currency',
  currency: CURRENCY_CODE,
  maximumFractionDigits: 2,
})

// Whole-currency formatter for large aggregates (dashboard KPIs) where cents add noise.
const compactCurrencyFormatter = new Intl.NumberFormat(undefined, {
  style: 'currency',
  currency: CURRENCY_CODE,
  maximumFractionDigits: 0,
})

/** Formats a monetary amount with 2 decimals, or an em dash when null/undefined. */
export function formatCurrency(value: number | null | undefined): string {
  if (value == null) return '—'
  return currencyFormatter.format(value)
}

/** Formats a monetary amount with no decimals — for large totals/KPIs. */
export function formatCurrencyCompact(value: number | null | undefined): string {
  if (value == null) return '—'
  return compactCurrencyFormatter.format(value)
}
