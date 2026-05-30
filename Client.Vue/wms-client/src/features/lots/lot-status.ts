import type { TagSeverity } from '@/lib/enum-display'
import type { LotDto } from '@/types/lots'

/** Derives a display status badge from a lot's expiry flags. */
export function lotStatus(
  lot: Pick<LotDto, 'isExpired' | 'isExpiringSoon'>,
): { label: string; severity: TagSeverity } {
  if (lot.isExpired) return { label: 'Expired', severity: 'danger' }
  if (lot.isExpiringSoon) return { label: 'Expiring soon', severity: 'warn' }
  return { label: 'OK', severity: 'success' }
}
