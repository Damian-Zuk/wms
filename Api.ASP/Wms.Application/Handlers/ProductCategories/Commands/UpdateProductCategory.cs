using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.ProductCategories.Commands;

public sealed record UpdateProductCategoryCommand(Guid Id, string Name, Guid? ParentId) : ICommand;

public sealed class UpdateProductCategoryValidator : AbstractValidator<UpdateProductCategoryCommand>
{
    public UpdateProductCategoryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Category ID is required");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
    }
}

public sealed class UpdateProductCategoryCommandHandler(IAppDbContext context)
    : ICommandHandler<UpdateProductCategoryCommand>
{
    public async Task<Result> Handle(
        UpdateProductCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var category = await context.ProductCategories
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category is null)
            return ProductCategoryErrors.NotFound(request.Id);

        if (request.ParentId is { } parentId)
        {
            var parentExists = await context.ProductCategories
                .AsNoTracking()
                .AnyAsync(c => c.Id == parentId, cancellationToken);

            if (!parentExists)
                return ProductCategoryErrors.ParentNotFound(parentId);

            // A category cannot become its own ancestor: the new parent must not be
            // the category itself nor any node within its subtree.
            var hierarchy = await CategoryHierarchy.LoadAsync(context, cancellationToken);
            if (hierarchy.DescendantIdsInclusive(request.Id).Contains(parentId))
                return ProductCategoryErrors.CircularReference;
        }

        category.Rename(request.Name.Trim());
        category.MoveTo(request.ParentId);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
