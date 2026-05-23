using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.Inventories.Queries;

public sealed record InventoryDto(
    Guid Id,
    Guid ProductId,
    Guid LocationId,
    Guid? LotId,
    int OnHand,
    int Reserved,
    int Available);

public sealed record GetInventoryQuery(Guid Id) : IQuery<InventoryDto>;

public sealed class GetInventoryQueryHandler(IAppDbContext context)
    : IQueryHandler<GetInventoryQuery, InventoryDto>
{
    public async Task<Result<InventoryDto>> Handle(
        GetInventoryQuery query,
        CancellationToken cancellationToken)
    {
        var inventory = await context.Inventories
            .AsNoTracking()
            .Where(i => i.Id == query.Id)
            .Select(i => new InventoryDto(
                i.Id,
                i.ProductId,
                i.LocationId,
                i.LotId,
                i.OnHand.Value,
                i.Reserved.Value,
                i.OnHand.Value - i.Reserved.Value))
            .FirstOrDefaultAsync(cancellationToken);

        return inventory is null ? InventoryErrors.NotFound(query.Id) : inventory;
    }
}
