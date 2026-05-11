using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Features.Products.Commands;

public sealed record CreateProductCommand(string Sku, string Name, string Description) : ICommand<Guid>;

public sealed class CreateProductValidator: AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().WithMessage("SKU is required");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
    }
}

public sealed class CreateProductCommandHandler(IAppDbContext context) 
    : ICommandHandler<CreateProductCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        var exists = await context.Products
            .AsNoTracking()
            .AnyAsync(p => p.Sku.Value == request.Sku, cancellationToken);

        if (exists)
            return ProductErrors.SkuExists(request.Sku);

        var product = new Product(new Sku(request.Sku), request.Name, request.Description);

        await context.Products.AddAsync(product, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
