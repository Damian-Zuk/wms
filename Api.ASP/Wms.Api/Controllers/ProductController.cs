using Microsoft.AspNetCore.Mvc;
using Wms.Api.Extensions;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Features.Products.Commands;
using Wms.Application.Features.Products.Queries;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListProducts(
        [FromQuery] string? search,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IQueryHandler<ListProductsQuery, PagedResult<ProductDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ListProductsQuery(
                search,
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken);
        return result.ToHttpResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> GetProduct(
        [FromRoute] Guid id,
        [FromServices] IQueryHandler<GetProductQuery, ProductDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetProductQuery(id), cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost]
    public async Task<IResult> CreateProduct(
        [FromBody] CreateProductCommand request,
        [FromServices] ICommandHandler<CreateProductCommand,  Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IResult> UpdateProduct(
        [FromRoute] Guid id,
        [FromBody] UpdateProductRequest request,
        [FromServices] ICommandHandler<UpdateProductCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new UpdateProductCommand(id, request.Name, request.Description),
            cancellationToken);
        return result.ToHttpResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IResult> DeleteProduct(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<DeleteProductCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteProductCommand(id), cancellationToken);
        return result.ToHttpResult();
    }

}

public sealed record UpdateProductRequest(string Name, string Description);
