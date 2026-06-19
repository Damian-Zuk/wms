using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Inventories.Queries;

public sealed record AvailabilityDto(
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid? LocationId,
    Guid? LotId,
    int OnHand,
    int Reserved,
    int Available,
    decimal UnitPrice,
    decimal OnHandValue);

public sealed record GetAvailabilityQuery(
    Guid ProductId,
    Guid? LocationId,
    Guid? LotId) : IQuery<AvailabilityDto>;


public sealed class GetAvailabilityQueryHandler(IAppDbContext context)
    : IQueryHandler<GetAvailabilityQuery, AvailabilityDto>
{
    public async Task<Result<AvailabilityDto>> Handle(
        GetAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        var product = await context.Products
            .AsNoTracking()
            .Where(p => p.Id == query.ProductId)
            .Select(p => new { Sku = p.Sku.Value, p.Name, p.UnitPrice })
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
            return ProductErrors.NotFound(query.ProductId);

        var inventories = context.Inventories
            .AsNoTracking()
            .Where(i => i.ProductId == query.ProductId);

        if (query.LocationId.HasValue)
            inventories = inventories.Where(i => i.LocationId == query.LocationId.Value);

        if (query.LotId.HasValue)
            inventories = inventories.Where(i => i.LotId == query.LotId.Value);

        var totals = await inventories
            .GroupBy(_ => 1)
            .Select(g => new
            {
                OnHand = g.Sum(i => i.OnHand.Value),
                Reserved = g.Sum(i => i.Reserved.Value)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var onHand = totals?.OnHand ?? 0;
        var reserved = totals?.Reserved ?? 0;

        return new AvailabilityDto(
            query.ProductId,
            product.Sku,
            product.Name,
            query.LocationId,
            query.LotId,
            onHand,
            reserved,
            onHand - reserved,
            product.UnitPrice,
            onHand * product.UnitPrice);
    }
}
