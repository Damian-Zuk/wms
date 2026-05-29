using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Putaway;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Features.Locations.Queries;

public sealed record PutawaySuggestionDto(
    Guid LocationId,
    string LocationCode,
    string LocationAddress,
    string StrategyName);

public sealed record SuggestPutawayLocationQuery(
    Guid ProductId,
    Guid? LotId,
    int Quantity) : IQuery<PutawaySuggestionDto>;

public sealed class SuggestPutawayLocationValidator : AbstractValidator<SuggestPutawayLocationQuery>
{
    public SuggestPutawayLocationValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
    }
}

public sealed class SuggestPutawayLocationQueryHandler(
    IPutawayService putawayService,
    IAppDbContext context)
    : IQueryHandler<SuggestPutawayLocationQuery, PutawaySuggestionDto>
{
    public async Task<Result<PutawaySuggestionDto>> Handle(
        SuggestPutawayLocationQuery query,
        CancellationToken cancellationToken)
    {
        var suggestion = await putawayService.SuggestAsync(
            query.ProductId,
            query.LotId,
            new Quantity(query.Quantity),
            cancellationToken);

        if (suggestion.IsFailure)
            return Result.Failure<PutawaySuggestionDto>(suggestion.Error);

        var locationId = suggestion.Value.LocationId;

        var location = await context.Locations
            .AsNoTracking()
            .Where(l => l.Id == locationId)
            .Select(l => new
            {
                l.Code.Value,
                l.Address.Zone,
                l.Address.Aisle,
                l.Address.Rack,
                l.Address.Shelf,
                l.Address.Bin
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (location is null)
            return LocationErrors.NotFound(locationId);

        var addressString = string.Join('-',
            location.Zone,
            location.Aisle,
            location.Rack,
            location.Shelf,
            location.Bin);

        return new PutawaySuggestionDto(
            locationId,
            location.Value,
            addressString,
            suggestion.Value.StrategyName);
    }
}
