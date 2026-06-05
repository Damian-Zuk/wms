using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Locations.Commands;

public sealed record CreateLocationCommand(
    string Code,
    string Zone,
    string Aisle,
    string Rack,
    string Shelf,
    string Bin,
    LocationType Type,
    string? Description,
    TemperatureZone TemperatureZone = TemperatureZone.Ambient,
    int? Capacity = null,
    decimal? WeightCapacity = null,
    decimal? VolumeCapacity = null,
    bool IsMixedSkuAllowed = true,
    bool IsMixedLotAllowed = true) : ICommand<Guid>;

public sealed class CreateLocationValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required");
        RuleFor(x => x.Zone).NotEmpty().WithMessage("Zone is required");
        RuleFor(x => x.Aisle).NotEmpty().WithMessage("Aisle is required");
        RuleFor(x => x.Rack).NotEmpty().WithMessage("Rack is required");
        RuleFor(x => x.Shelf).NotEmpty().WithMessage("Shelf is required");
        RuleFor(x => x.Bin).NotEmpty().WithMessage("Bin is required");
        RuleFor(x => x.Type).IsInEnum().WithMessage("Type must be a valid LocationType");
        RuleFor(x => x.TemperatureZone).IsInEnum().WithMessage("TemperatureZone must be a valid value");
        RuleFor(x => x.Capacity)
            .GreaterThan(0).When(x => x.Capacity.HasValue)
            .WithMessage("Capacity must be greater than 0 when provided");
        RuleFor(x => x.WeightCapacity)
            .GreaterThan(0).When(x => x.WeightCapacity.HasValue)
            .WithMessage("Weight capacity must be greater than 0 when provided");
        RuleFor(x => x.VolumeCapacity)
            .GreaterThan(0).When(x => x.VolumeCapacity.HasValue)
            .WithMessage("Volume capacity must be greater than 0 when provided");
    }
}

public sealed class CreateLocationCommandHandler(IAppDbContext context)
    : ICommandHandler<CreateLocationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateLocationCommand request, CancellationToken cancellationToken)
    {
        var codeExists = await context.Locations
            .AsNoTracking()
            .AnyAsync(l => l.Code.Value == request.Code, cancellationToken);

        if (codeExists)
            return LocationErrors.CodeExists(request.Code);

        var addressResult = LocationAddress.Create(
            request.Zone,
            request.Aisle,
            request.Rack,
            request.Shelf,
            request.Bin);

        if (addressResult.IsFailure)
            return Result.Failure<Guid>(addressResult.Error);

        var address = addressResult.Value;

        var addressExists = await context.Locations
            .AsNoTracking()
            .AnyAsync(
                l => l.Address.Zone == address.Zone
                    && l.Address.Aisle == address.Aisle
                    && l.Address.Rack == address.Rack
                    && l.Address.Shelf == address.Shelf
                    && l.Address.Bin == address.Bin,
                cancellationToken);

        if (addressExists)
            return LocationErrors.AddressExists(address.ToString());

        var location = new Location(
            new LocationCode(request.Code),
            address,
            request.Type,
            request.Description,
            request.TemperatureZone,
            request.Capacity,
            request.IsMixedSkuAllowed,
            request.IsMixedLotAllowed,
            request.WeightCapacity,
            request.VolumeCapacity);

        await context.Locations.AddAsync(location, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return location.Id;
    }
}
