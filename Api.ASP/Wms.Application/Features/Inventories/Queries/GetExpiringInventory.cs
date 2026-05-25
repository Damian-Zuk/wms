using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Shared.Common;

namespace Wms.Application.Features.Inventories.Queries;

public sealed record ExpiringInventoryLineDto(
    Guid InventoryId,
    Guid ProductId,
    string ProductSku,
    Guid LotId,
    string LotNumber,
    DateOnly ExpirationDate,
    Guid LocationId,
    string LocationCode,
    int Available);

public sealed record GetExpiringInventoryQuery(int WithinDays)
    : IQuery<IReadOnlyList<ExpiringInventoryLineDto>>;

public sealed class GetExpiringInventoryValidator : AbstractValidator<GetExpiringInventoryQuery>
{
    public GetExpiringInventoryValidator()
    {
        RuleFor(x => x.WithinDays).GreaterThanOrEqualTo(0)
            .WithMessage("WithinDays must be zero or positive");
    }
}

public sealed class GetExpiringInventoryQueryHandler(IAppDbContext context)
    : IQueryHandler<GetExpiringInventoryQuery, IReadOnlyList<ExpiringInventoryLineDto>>
{
    public async Task<Result<IReadOnlyList<ExpiringInventoryLineDto>>> Handle(
        GetExpiringInventoryQuery query,
        CancellationToken cancellationToken)
    {
        var threshold = DateOnly.FromDateTime(DateTime.Today).AddDays(query.WithinDays);

        var rows = await (
            from i in context.Inventories.AsNoTracking()
            join lot in context.Lots.AsNoTracking() on i.LotId equals lot.Id
            join product in context.Products.AsNoTracking() on i.ProductId equals product.Id
            join location in context.Locations.AsNoTracking() on i.LocationId equals location.Id
            where lot.ExpirationDate != null
                && lot.ExpirationDate.Value <= threshold
                && i.OnHand.Value - i.Reserved.Value > 0
            orderby lot.ExpirationDate
            select new ExpiringInventoryLineDto(
                i.Id,
                product.Id,
                product.Sku.Value,
                lot.Id,
                lot.Number.Value,
                lot.ExpirationDate!.Value,
                location.Id,
                location.Code.Value,
                i.OnHand.Value - i.Reserved.Value))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ExpiringInventoryLineDto>>(rows);
    }
}
