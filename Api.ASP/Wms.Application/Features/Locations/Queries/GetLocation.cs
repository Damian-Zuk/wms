using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Application.Extensions;
using Wms.Shared.Common;

namespace Wms.Application.Features.Locations.Queries;

public sealed record LocationDto(Guid Id, string Code, string? Description);

public sealed record GetLocationQuery(Guid Id) : IQuery<LocationDto>;

public sealed class GetLocationQueryHandler(IAppDbContext context)
    : IQueryHandler<GetLocationQuery, LocationDto>
{
    public async Task<Result<LocationDto>> Handle(GetLocationQuery query, CancellationToken cancellationToken)
    {
        var location = await context.Locations
            .AsNoTracking()
            .Where(l => l.Id == query.Id)
            .Select(l => new LocationDto(l.Id, l.Code.Value, l.Description))
            .FirstOrDefaultAsync(cancellationToken);

        return location is null ? Error.NotFound : location;
    }
}
