using Wms.Domain.Entities;
using Wms.Domain.ValueObjects;

namespace Wms.Application.Putaway;

/// <summary>
/// A single link in the putaway suggestion chain. Strategies are read-only:
/// they MUST NOT mutate inventory or location state. Returning null means
/// "no suggestion from this strategy; try the next one". A non-null suggestion
/// is NOT a commitment — the caller must still validate with Location.CanAccept
/// before assigning, since state can race between suggestion and assignment.
/// </summary>
public interface IPutawayStrategy
{
    string Name { get; }

    Task<PutawaySuggestion?> SuggestAsync(
        Product product,
        Lot? lot,
        Quantity quantity,
        CancellationToken ct);
}
