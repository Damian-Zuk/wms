using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Locations.Commands;

/// <summary>
/// Sets which products treat this location as a preferred location. The
/// relationship is owned by <c>Product.PreferredLocations</c>, so this command
/// reconciles that collection across the affected products in a single unit of
/// work: the location is added to newly-selected products and removed from
/// products that are no longer selected.
/// </summary>
public sealed record SetLocationPreferredProductsCommand(
    Guid LocationId,
    IReadOnlyList<Guid> ProductIds) : ICommand;

public sealed class SetLocationPreferredProductsValidator
    : AbstractValidator<SetLocationPreferredProductsCommand>
{
    public SetLocationPreferredProductsValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty().WithMessage("Location ID is required");
        RuleForEach(x => x.ProductIds)
            .NotEqual(Guid.Empty).WithMessage("Product IDs must be non-empty GUIDs");
    }
}

public sealed class SetLocationPreferredProductsCommandHandler(IAppDbContext context)
    : ICommandHandler<SetLocationPreferredProductsCommand>
{
    public async Task<Result> Handle(
        SetLocationPreferredProductsCommand request,
        CancellationToken cancellationToken)
    {
        var locationExists = await context.Locations
            .AsNoTracking()
            .AnyAsync(l => l.Id == request.LocationId, cancellationToken);

        if (!locationExists)
            return LocationErrors.NotFound(request.LocationId);

        var targetIds = request.ProductIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet();

        if (targetIds.Count > 0)
        {
            var existingIds = await context.Products
                .AsNoTracking()
                .Where(p => targetIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            var missing = targetIds.Except(existingIds).FirstOrDefault();
            if (missing != default)
                return ProductErrors.NotFound(missing);
        }

        // Load every product that is either newly selected or currently linked to
        // this location, so we can add/remove the link as needed.
        var affected = await context.Products
            .Include(p => p.PreferredLocations)
            .Where(p =>
                targetIds.Contains(p.Id)
                || p.PreferredLocations.Any(pl => pl.LocationId == request.LocationId))
            .ToListAsync(cancellationToken);

        foreach (var product in affected)
        {
            var currentIds = product.PreferredLocations
                .OrderBy(pl => pl.Sequence)
                .Select(pl => pl.LocationId)
                .ToList();

            var shouldHave = targetIds.Contains(product.Id);
            var hasLink = currentIds.Contains(request.LocationId);

            if (shouldHave && !hasLink)
            {
                currentIds.Add(request.LocationId);
                product.SetPreferredLocations(currentIds);
            }
            else if (!shouldHave && hasLink)
            {
                currentIds.Remove(request.LocationId);
                product.SetPreferredLocations(currentIds);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
