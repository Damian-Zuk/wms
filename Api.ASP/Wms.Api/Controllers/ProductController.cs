using Microsoft.AspNetCore.Mvc;
using Wms.Api.Extensions;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Features.Products.Commands;
using Wms.Application.Features.Products.Queries;
using Wms.Shared.Common;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ProductDto>> GetProducts(
        [FromServices] IQueryHandler<GetProductsQuery, IEnumerable<ProductDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetProductsQuery(), cancellationToken);
        return result.Match(products => products, _ => []);
    }

    [HttpPost]
    public async Task<IResult> CreateProduct(
        [FromBody] CreateProductCommand request,
        [FromServices] ICommandHandler<CreateProductCommand,  Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.IsSuccess ?  Results.Ok(result.Value) : result.ToProblemDetails();
    }
}
