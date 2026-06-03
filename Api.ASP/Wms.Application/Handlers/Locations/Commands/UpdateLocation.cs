using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Locations.Commands;

public sealed record UpdateLocationCommand(
    Guid Id,
    string Code,
    string Zone,
    string Aisle,
    string Rack,
    string Shelf,
    string Bin,
    LocationType Type,
    string? Description,
    TemperatureZone TemperatureZone,
    int? Capacity,
    bool IsMixedSkuAllowed,
    bool IsMixedLotAllowed) : ICommand;

public sealed class UpdateLocationValidator : AbstractValidator<UpdateLocationCommand>
{
    public UpdateLocationValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Location ID is required");
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
    }
}

public sealed class UpdateLocationCommandHandler(IAppDbContext context)
    : ICommandHandler<UpdateLocationCommand>
{
    public async Task<Result> Handle(UpdateLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await context.Locations
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (location is null)
            return LocationErrors.NotFound(request.Id);

        var codeConflict = await context.Locations
            .AsNoTracking()
            .AnyAsync(l => l.Id != request.Id && l.Code.Value == request.Code, cancellationToken);

        if (codeConflict)
            return LocationErrors.CodeExists(request.Code);

        var addressResult = LocationAddress.Create(
            request.Zone,
            request.Aisle,
            request.Rack,
            request.Shelf,
            request.Bin);

        if (addressResult.IsFailure)
            return addressResult.Error;

        var address = addressResult.Value;

        var addressConflict = await context.Locations
            .AsNoTracking()
            .AnyAsync(
                l => l.Id != request.Id
                    && l.Address.Zone == address.Zone
                    && l.Address.Aisle == address.Aisle
                    && l.Address.Rack == address.Rack
                    && l.Address.Shelf == address.Shelf
                    && l.Address.Bin == address.Bin,
                cancellationToken);

        if (addressConflict)
            return LocationErrors.AddressExists(address.ToString());

        location.Update(
            new LocationCode(request.Code),
            address,
            request.Type,
            request.Description,
            request.TemperatureZone,
            request.Capacity,
            request.IsMixedSkuAllowed,
            request.IsMixedLotAllowed);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
