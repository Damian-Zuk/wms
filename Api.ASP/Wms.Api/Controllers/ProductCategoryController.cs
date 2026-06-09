using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Common.Messaging;
using Wms.Application.Handlers.ProductCategories.Commands;
using Wms.Application.Handlers.ProductCategories.Queries;
using Wms.Shared.Common;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/product-categories")]
public class ProductCategoryController : ControllerBase
{
    [HttpGet("tree")]
    public async Task<IResult> GetTree(
        [FromServices] IQueryHandler<GetProductCategoryTreeQuery, IReadOnlyList<CategoryTreeNodeDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetProductCategoryTreeQuery(), cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet]
    public async Task<IResult> ListCategories(
        [FromServices] IQueryHandler<ListProductCategoriesQuery, IReadOnlyList<ProductCategoryDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListProductCategoriesQuery(), cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    public async Task<IResult> CreateCategory(
        [FromBody] CreateProductCategoryCommand request,
        [FromServices] ICommandHandler<CreateProductCategoryCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPut("{id:guid}")]
    public async Task<IResult> UpdateCategory(
        [FromRoute] Guid id,
        [FromBody] UpdateProductCategoryRequest request,
        [FromServices] ICommandHandler<UpdateProductCategoryCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new UpdateProductCategoryCommand(id, request.Name, request.ParentId),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpDelete("{id:guid}")]
    public async Task<IResult> DeleteCategory(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<DeleteProductCategoryCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteProductCategoryCommand(id), cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}

public sealed record UpdateProductCategoryRequest(string Name, Guid? ParentId);
