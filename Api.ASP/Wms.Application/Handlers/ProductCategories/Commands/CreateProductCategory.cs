using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.ProductCategories.Commands;

public sealed record CreateProductCategoryCommand(string Name, Guid? ParentId) : ICommand<Guid>;

public sealed class CreateProductCategoryValidator : AbstractValidator<CreateProductCategoryCommand>
{
    public CreateProductCategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
    }
}

public sealed class CreateProductCategoryCommandHandler(IAppDbContext context)
    : ICommandHandler<CreateProductCategoryCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateProductCategoryCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ParentId is { } parentId)
        {
            var parentExists = await context.ProductCategories
                .AsNoTracking()
                .AnyAsync(c => c.Id == parentId, cancellationToken);

            if (!parentExists)
                return ProductCategoryErrors.ParentNotFound(parentId);
        }

        var category = new ProductCategory(request.Name.Trim(), request.ParentId);

        await context.ProductCategories.AddAsync(category, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}
