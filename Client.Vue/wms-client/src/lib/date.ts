/** Converts a Date (local) to the API's DateOnly string "yyyy-MM-dd". */
export function toDateOnly(date: Date | null): string | null {
  if (!date) return null
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/** Parses an API DateOnly string "yyyy-MM-dd" to a local Date. */
export function fromDateOnly(value: string | null): Date | null {
  if (!value) return null
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day)
}

/** Display helper: returns the date string or an em dash when null. */
export function formatDate(value: string | null): string {
  return value ?? '—'
}
