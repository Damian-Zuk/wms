using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Shared.Common;

namespace Wms.Application.Features.Inventories.Queries;

public sealed record ListInventoriesQuery(
    Guid? ProductId,
    Guid? LocationId,
    Guid? LotId,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<InventoryDto>>;

public sealed class ListInventoriesValidator : AbstractValidator<ListInventoriesQuery>
{
    public ListInventoriesValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

public sealed class ListInventoriesQueryHandler(IAppDbContext context)
    : IQueryHandler<ListInventoriesQuery, PagedResult<InventoryDto>>
{
    public async Task<Result<PagedResult<InventoryDto>>> Handle(
        ListInventoriesQuery query,
        CancellationToken cancellationToken)
    {
        var inventoriesQuery = context.Inventories.AsNoTracking().AsQueryable();

        if (query.ProductId.HasValue)
            inventoriesQuery = inventoriesQuery.Where(i => i.ProductId == query.ProductId.Value);

        if (query.LocationId.HasValue)
            inventoriesQuery = inventoriesQuery.Where(i => i.LocationId == query.LocationId.Value);

        if (query.LotId.HasValue)
            inventoriesQuery = inventoriesQuery.Where(i => i.LotId == query.LotId.Value);

        var totalCount = await inventoriesQuery.CountAsync(cancellationToken);

        var items = await inventoriesQuery
            .OrderBy(i => i.ProductId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(i => new InventoryDto(
                i.Id,
                i.ProductId,
                i.LocationId,
                i.LotId,
                i.OnHand.Value,
                i.Reserved.Value,
                i.OnHand.Value - i.Reserved.Value))
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryDto>(items, query.Page, query.PageSize, totalCount);
    }
}
