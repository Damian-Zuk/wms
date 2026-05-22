using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Putaway;

internal sealed class PutawayService(
    IAppDbContext context,
    IEnumerable<IPutawayStrategy> strategies)
    : IPutawayService
{
    public async Task<Result<PutawaySuggestion>> SuggestAsync(
        Guid productId,
        Guid? lotId,
        Quantity quantity,
        CancellationToken ct)
    {
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null)
            return PutawayErrors.ProductNotFound(productId);

        Lot? lot = null;
        if (lotId.HasValue)
        {
            lot = await context.Lots
                .FirstOrDefaultAsync(l => l.Id == lotId.Value, ct);

            if (lot is null)
                return PutawayErrors.LotNotFound(lotId.Value);
        }

        foreach (var strategy in strategies)
        {
            var suggestion = await strategy.SuggestAsync(product, lot, quantity, ct);
            if (suggestion is not null)
                return suggestion;
        }

        return PutawayErrors.NoSuitablePutawayLocation(productId, lotId, quantity.Value);
    }
}
