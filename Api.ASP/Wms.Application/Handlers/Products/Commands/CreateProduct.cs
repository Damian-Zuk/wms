using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Products.Commands;

public sealed record CreateProductCommand(
    string Sku,
    string Name,
    string Description,
    decimal Weight,
    decimal Volume,
    TemperatureZone RequiredTemperatureZone = TemperatureZone.Ambient,
    IReadOnlyList<Guid>? PreferredLocationIds = null) : ICommand<Guid>;

public sealed class CreateProductValidator: AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().WithMessage("SKU is required");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
        RuleFor(x => x.Weight).GreaterThan(0).WithMessage("Weight must be greater than 0");
        RuleFor(x => x.Volume).GreaterThan(0).WithMessage("Volume must be greater than 0");
        RuleFor(x => x.RequiredTemperatureZone)
            .IsInEnum().WithMessage("RequiredTemperatureZone must be a valid value");
        RuleForEach(x => x.PreferredLocationIds!)
            .NotEqual(Guid.Empty).WithMessage("Preferred location IDs must be non-empty GUIDs")
            .When(x => x.PreferredLocationIds is not null);
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

        var distinctPreferred = (request.PreferredLocationIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctPreferred.Count > 0)
        {
            var existingIds = await context.Locations
                .AsNoTracking()
                .Where(l => distinctPreferred.Contains(l.Id))
                .Select(l => l.Id)
                .ToListAsync(cancellationToken);

            var missing = distinctPreferred.Except(existingIds).FirstOrDefault();
            if (missing != default)
                return LocationErrors.NotFound(missing);
        }

        var product = new Product(
            new Sku(request.Sku),
            request.Name,
            request.Weight,
            request.Volume,
            request.Description,
            request.RequiredTemperatureZone);

        product.SetPreferredLocations(distinctPreferred);

        await context.Products.AddAsync(product, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
