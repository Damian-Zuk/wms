using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.Inventories.Queries;

public sealed record AvailabilityDto(
    Guid ProductId,
    Guid? LocationId,
    Guid? LotId,
    int OnHand,
    int Reserved,
    int Available);

public sealed record GetAvailabilityQuery(
    Guid ProductId,
    Guid? LocationId,
    Guid? LotId) : IQuery<AvailabilityDto>;

public sealed class GetAvailabilityValidator : AbstractValidator<GetAvailabilityQuery>
{
    public GetAvailabilityValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required");
    }
}

public sealed class GetAvailabilityQueryHandler(IAppDbContext context)
    : IQueryHandler<GetAvailabilityQuery, AvailabilityDto>
{
    public async Task<Result<AvailabilityDto>> Handle(
        GetAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        var productExists = await context.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == query.ProductId, cancellationToken);

        if (!productExists)
            return ProductErrors.NotFound(query.ProductId);

        var inventories = context.Inventories
            .AsNoTracking()
            .Where(i => i.ProductId == query.ProductId);

        if (query.LocationId.HasValue)
            inventories = inventories.Where(i => i.LocationId == query.LocationId.Value);

        if (query.LotId.HasValue)
            inventories = inventories.Where(i => i.LotId == query.LotId.Value);

        var totals = await inventories
            .GroupBy(_ => 1)
            .Select(g => new
            {
                OnHand = g.Sum(i => i.OnHand.Value),
                Reserved = g.Sum(i => i.Reserved.Value)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var onHand = totals?.OnHand ?? 0;
        var reserved = totals?.Reserved ?? 0;

        return new AvailabilityDto(
            query.ProductId,
            query.LocationId,
            query.LotId,
            onHand,
            reserved,
            onHand - reserved);
    }
}
