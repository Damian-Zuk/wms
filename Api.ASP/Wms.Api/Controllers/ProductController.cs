using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Handlers.Products.Commands;
using Wms.Application.Handlers.Products.Queries;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListProducts(
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] bool sortDescending,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] Guid? categoryId,
        [FromServices] IQueryHandler<ListProductsQuery, PagedResult<ProductDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ListProductsQuery(
                search,
                sortBy,
                sortDescending,
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize,
                categoryId),
            cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> GetProduct(
        [FromRoute] Guid id,
        [FromServices] IQueryHandler<GetProductQuery, ProductDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetProductQuery(id), cancellationToken);
        
        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    public async Task<IResult> CreateProduct(
        [FromBody] CreateProductCommand request,
        [FromServices] ICommandHandler<CreateProductCommand,  Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPut("{id:guid}")]
    public async Task<IResult> UpdateProduct(
        [FromRoute] Guid id,
        [FromBody] UpdateProductRequest request,
        [FromServices] ICommandHandler<UpdateProductCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new UpdateProductCommand(
                id,
                request.Name,
                request.Description,
                request.Weight,
                request.Volume,
                request.UnitPrice,
                request.RequiredTemperatureZone,
                request.PreferredLocationIds,
                request.CategoryId),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpDelete("{id:guid}")]
    public async Task<IResult> DeleteProduct(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<DeleteProductCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteProductCommand(id), cancellationToken);
        
        return result.Match(Results.NoContent, CustomResults.Problem);
    }

}

public sealed record UpdateProductRequest(
    string Name,
    string Description,
    decimal Weight,
    decimal Volume,
    decimal UnitPrice,
    TemperatureZone RequiredTemperatureZone,
    IReadOnlyList<Guid>? PreferredLocationIds,
    Guid? CategoryId);
