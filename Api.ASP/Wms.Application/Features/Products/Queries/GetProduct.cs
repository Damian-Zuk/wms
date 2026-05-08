using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Application.Extensions;
using Wms.Shared.Common;

namespace Wms.Application.Features.Products.Queries;

public sealed record GetProductQuery(Guid Id) : IQuery<ProductDto>;

public sealed class GetProductQueryHandler(IAppDbContext context)
    : IQueryHandler<GetProductQuery, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(GetProductQuery query, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .AsNoTracking()
            .Select(p => new ProductDto(p.Id, p.Sku.Value, p.Name, p.Description))
            .FirstOrDefaultAsync(cancellationToken);

        return product is null ? Error.NotFound : product;
    }
}
