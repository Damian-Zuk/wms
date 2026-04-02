
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Shared.Common;

namespace Wms.Application.Features.Products.Queries;

public record ProductDto(Guid Id, string Sku, string Name, string Description);

public record GetProductsQuery : IQuery<IEnumerable<ProductDto>>;

public class GetProductsQueryHandler(IAppDbContext context) 
    : IQueryHandler<GetProductsQuery, IEnumerable<ProductDto>>
{
    public async Task<Result<IEnumerable<ProductDto>>> Handle(
        GetProductsQuery query,
        CancellationToken cancellationToken)
    {
        var products = await context.Products
            .AsNoTracking()
            .Select(p => new ProductDto(p.Id, p.Sku.Value, p.Name, p.Description))
            .ToListAsync(cancellationToken);
    
        return Result.Success(products.AsEnumerable());
    }
}
