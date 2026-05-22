using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Putaway;

public interface IPutawayService
{
    Task<Result<PutawaySuggestion>> SuggestAsync(
        Guid productId,
        Guid? lotId,
        Quantity quantity,
        CancellationToken ct);
}
