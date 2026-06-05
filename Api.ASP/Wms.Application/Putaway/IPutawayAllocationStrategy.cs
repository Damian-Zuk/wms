using Wms.Domain.Entities;
using Wms.Domain.Enums;

namespace Wms.Application.Putaway;

/// <summary>
/// A link in the multi-location putaway chain. Unlike <see cref="IPutawayStrategy"/>
/// (which suggests a single location), an allocation strategy only RANKS candidate
/// locations for a product/lot over a pre-loaded <see cref="PutawayPlanContext"/>
/// snapshot — the planner decides how much each candidate receives. Strategies are
/// pure and read-only: no DB access, no mutation. The planner still validates every
/// candidate with <see cref="Location.CanAccept(Product, Lot?, ValueObjects.Quantity, IEnumerable{Inventory}, Services.CapacityOccupancy, IReadOnlyDictionary{Guid, Product})"/>.
/// </summary>
public interface IPutawayAllocationStrategy
{
    PutawayStrategyType Type { get; }

    IReadOnlyList<Guid> CandidateLocations(Product product, Lot? lot, PutawayPlanContext context);
}
