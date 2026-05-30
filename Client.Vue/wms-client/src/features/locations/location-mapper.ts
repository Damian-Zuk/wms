import type { CreateLocationCommand, LocationFormValues } from '@/types/locations'

/** Maps form values to a create/update request body (create and update share a shape). */
export function toCommand(values: LocationFormValues): CreateLocationCommand {
  return {
    code: values.code,
    zone: values.zone,
    aisle: values.aisle,
    rack: values.rack,
    shelf: values.shelf,
    bin: values.bin,
    type: values.type,
    description: values.description.trim() || null,
    temperatureZone: values.temperatureZone,
    capacity: values.capacity,
    isMixedSkuAllowed: values.isMixedSkuAllowed,
    isMixedLotAllowed: values.isMixedLotAllowed,
  }
}
