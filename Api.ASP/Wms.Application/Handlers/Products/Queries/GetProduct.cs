using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Products.Queries;

public sealed record GetProductQuery(Guid Id) : IQuery<ProductDto>;

public sealed class GetProductQueryHandler(IAppDbContext context)
    : IQueryHandler<GetProductQuery, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(GetProductQuery query, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .AsNoTracking()
            .Where(p => p.Id == query.Id)
            .Select(p => new ProductDto(
                p.Id,
                p.Sku.Value,
                p.Name,
                p.Description,
                p.Weight,
                p.Volume,
                p.RequiredTemperatureZone,
                p.PreferredLocations
                    .OrderBy(pl => pl.Sequence)
                    .Select(pl => pl.LocationId)
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return product is null ? ProductErrors.NotFound(query.Id) : product;
    }
}
