using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Extensions;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Lots.Queries;

public sealed record LotDto(
    Guid Id,
    string Number,
    Guid ProductId,
    DateOnly? ManufactureDate,
    DateOnly? ExpirationDate,
    bool IsExpired,
    bool IsExpiringSoon);

public sealed record GetLotQuery(Guid Id) : IQuery<LotDto>;

public sealed class GetLotQueryHandler(IAppDbContext context)
    : IQueryHandler<GetLotQuery, LotDto>
{
    public async Task<Result<LotDto>> Handle(GetLotQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var soonThreshold = today.AddDays(30);

        var lot = await context.Lots
            .AsNoTracking()
            .Where(l => l.Id == query.Id)
            .Select(l => new LotDto(
                l.Id,
                l.Number.Value,
                l.ProductId,
                l.ManufactureDate,
                l.ExpirationDate,
                l.ExpirationDate != null && l.ExpirationDate.Value < today,
                l.ExpirationDate != null && l.ExpirationDate.Value <= soonThreshold))
            .FirstOrDefaultAsync(cancellationToken);

        return lot is null ? LotErrors.NotFound(query.Id) : lot;
    }
}
