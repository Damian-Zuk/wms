using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.ProductCategories.Commands;

public sealed record DeleteProductCategoryCommand(Guid Id) : ICommand;

public sealed class DeleteProductCategoryCommandHandler(IAppDbContext context)
    : ICommandHandler<DeleteProductCategoryCommand>
{
    public async Task<Result> Handle(
        DeleteProductCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var category = await context.ProductCategories
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category is null)
            return ProductCategoryErrors.NotFound(request.Id);

        // Promote direct children to the deleted node's parent (they keep their
        // own subtrees), so the tree stays connected.
        var children = await context.ProductCategories
            .Where(c => c.ParentId == request.Id)
            .ToListAsync(cancellationToken);

        foreach (var child in children)
            child.MoveTo(category.ParentId);

        // Detach products from the category rather than deleting them.
        var products = await context.Products
            .Where(p => p.ProductCategoryId == request.Id)
            .ToListAsync(cancellationToken);

        foreach (var product in products)
            product.SetCategory(null);

        category.MarkAsDeleted();

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
