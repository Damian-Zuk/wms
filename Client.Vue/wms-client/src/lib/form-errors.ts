/**
 * Maps server-side ProblemDetails field errors (keyed by FluentValidation's
 * PascalCase PropertyName) onto camelCase form field names, joining multiple
 * messages per field. The result is ready to hand to vee-validate's setErrors.
 */
export function mapServerErrors(
  serverErrors: Record<string, string[]> | undefined,
): Record<string, string> {
  const mapped: Record<string, string> = {}
  if (!serverErrors) return mapped
  for (const [key, messages] of Object.entries(serverErrors)) {
    const field = key.charAt(0).toLowerCase() + key.slice(1)
    mapped[field] = messages.join(', ')
  }
  return mapped
}
