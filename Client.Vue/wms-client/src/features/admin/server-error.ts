import type { AppError } from '@/api/problem-details'

/**
 * The accounts endpoints surface Identity failures as a bare `errors` array of
 * description strings (e.g. password-policy violations), which `normalizeError`
 * stores under `fieldErrors`. Flatten whatever shape we get into one message.
 */
export function serverErrorText(err: AppError): string {
  const fe = err.fieldErrors as unknown
  const messages = Array.isArray(fe)
    ? (fe as string[])
    : Object.values(err.fieldErrors).flat()

  return messages.length ? messages.join(' ') : err.message
}
