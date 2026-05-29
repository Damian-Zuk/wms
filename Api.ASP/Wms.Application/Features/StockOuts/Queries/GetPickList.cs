using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockOuts.Queries;

public sealed record PickListLineDto(
    Guid StockOutItemId,
    Guid ProductId,
    string ProductSku,
    Guid LocationId,
    string LocationCode,
    string LocationAddress,
    Guid? LotId,
    string? LotNumber,
    int Quantity);

public sealed record PickListDto(
    Guid StockOutId,
    IReadOnlyList<PickListLineDto> Lines);

public sealed record GetPickListQuery(Guid StockOutId) : IQuery<PickListDto>;

public sealed class GetPickListQueryHandler(IAppDbContext context)
    : IQueryHandler<GetPickListQuery, PickListDto>
{
    public async Task<Result<PickListDto>> Handle(
        GetPickListQuery query,
        CancellationToken cancellationToken)
    {
        var stockOut = await context.StockOuts
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == query.StockOutId, cancellationToken);

        if (stockOut is null)
            return StockOutErrors.NotFound(query.StockOutId);

        var productIds = stockOut.Items.Select(i => i.ProductId).Distinct().ToList();
        var locationIds = stockOut.Items.Select(i => i.LocationId).Distinct().ToList();
        var lotIds = stockOut.Items
            .Where(i => i.LotId.HasValue)
            .Select(i => i.LotId!.Value)
            .Distinct()
            .ToList();

        var products = await context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, Sku = p.Sku.Value })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var locations = await context.Locations
            .AsNoTracking()
            .Where(l => locationIds.Contains(l.Id))
            .Select(l => new
            {
                l.Id,
                Code = l.Code.Value,
                l.Address.Zone,
                l.Address.Aisle,
                l.Address.Rack,
                l.Address.Shelf,
                l.Address.Bin
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var lotNumbersById = await context.Lots
            .AsNoTracking()
            .Where(l => lotIds.Contains(l.Id))
            .Select(l => new { l.Id, Number = l.Number.Value })
            .ToDictionaryAsync(x => x.Id, x => x.Number, cancellationToken);

        var ordered = stockOut.Items
            .Select(item =>
            {
                var loc = locations[item.LocationId];
                return new
                {
                    Item = item,
                    loc.Zone,
                    loc.Aisle,
                    loc.Rack,
                    loc.Shelf,
                    loc.Bin
                };
            })
            .OrderBy(x => x.Zone, StringComparer.Ordinal)
                .ThenBy(x => x.Aisle, StringComparer.Ordinal)
                .ThenBy(x => x.Rack, StringComparer.Ordinal)
                .ThenBy(x => x.Shelf, StringComparer.Ordinal)
                .ThenBy(x => x.Bin, StringComparer.Ordinal)
            .ToList();

        var lines = ordered.Select(x =>
        {
            var item = x.Item;
            var loc = locations[item.LocationId];
            var sku = products.TryGetValue(item.ProductId, out var p) ? p.Sku : string.Empty;
            string? lotNumber = null;
            if (item.LotId.HasValue && lotNumbersById.TryGetValue(item.LotId.Value, out var num))
                lotNumber = num;

            var address = string.Join('-', loc.Zone, loc.Aisle, loc.Rack, loc.Shelf, loc.Bin);

            return new PickListLineDto(
                item.Id,
                item.ProductId,
                sku,
                item.LocationId,
                loc.Code,
                address,
                item.LotId,
                lotNumber,
                item.Quantity.Value);
        }).ToList();

        return new PickListDto(stockOut.Id, lines);
    }
}
