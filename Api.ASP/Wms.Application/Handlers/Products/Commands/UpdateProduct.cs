using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Products.Commands;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string Description,
    decimal Weight,
    decimal Volume,
    TemperatureZone RequiredTemperatureZone,
    IReadOnlyList<Guid>? PreferredLocationIds) : ICommand;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Product ID is required");
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

public sealed class UpdateProductCommandHandler(IAppDbContext context)
    : ICommandHandler<UpdateProductCommand>
{
    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .Include(p => p.PreferredLocations)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
            return ProductErrors.NotFound(request.Id);

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

        product.Update(request.Name, request.Description, request.Weight, request.Volume, request.RequiredTemperatureZone);
        product.SetPreferredLocations(distinctPreferred);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
