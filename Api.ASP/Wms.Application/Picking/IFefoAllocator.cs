using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Picking;

public sealed record LotAllocation(Guid LotId, Quantity Quantity);

/// <summary>
/// First-Expired-First-Out lot allocator. Given a product, an optional
/// location scope, and a required quantity, returns the list of lots to
/// draw from. The allocator is a pure read — it never mutates 
/// inventory or creates reservations.
/// </summary>
public interface IFefoAllocator
{
    Task<Result<IReadOnlyList<LotAllocation>>> AllocateAsync(
        Guid productId,
        Guid? locationId,
        Quantity required,
        CancellationToken ct);
}
