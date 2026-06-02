using Wms.Shared.Common;

namespace Wms.Application.Putaway;

/// <summary>
/// Atomically verifies and reserves location capacity when a stock-in starts
/// receiving. Implemented in Infrastructure because it needs a database
/// transaction and row-level locking that the application abstraction doesn't expose.
/// </summary>
public interface ICapacityReservationService
{
    Task<Result> ReserveForStartReceivingAsync(Guid stockInId, CancellationToken ct);
}
