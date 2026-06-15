using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.HandlingUnits;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.HandlingUnits.Commands;

public sealed record CreateHandlingUnitCommand(
    Guid LocationId,
    HandlingUnitType Type,
    string? Code = null) : ICommand<Guid>;

public sealed class CreateHandlingUnitValidator : AbstractValidator<CreateHandlingUnitCommand>
{
    public CreateHandlingUnitValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty().WithMessage("Location ID is required");
        RuleFor(x => x.Type).IsInEnum().WithMessage("Handling unit type is invalid");
        RuleFor(x => x.Code).MaximumLength(50).When(x => x.Code != null)
            .WithMessage("Code must not exceed 50 characters");
    }
}

/// <summary>
/// Creates an empty handling unit standing at a location, ready to be packed.
/// The license-plate code is generated unless the caller supplies one. An empty
/// unit consumes no capacity, so no capacity check is made.
/// </summary>
public sealed class CreateHandlingUnitCommandHandler(
    IAppDbContext context,
    IHandlingUnitCodeGenerator codeGenerator)
    : ICommandHandler<CreateHandlingUnitCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateHandlingUnitCommand command, CancellationToken cancellationToken)
    {
        var locationExists = await context.Locations
            .AnyAsync(l => l.Id == command.LocationId, cancellationToken);

        if (!locationExists)
            return HandlingUnitErrors.LocationNotFound(command.LocationId);

        if (command.Code is not null)
        {
            var codeTaken = await context.HandlingUnits
                .AnyAsync(h => h.Code.Value == command.Code, cancellationToken);

            if (codeTaken)
                return HandlingUnitErrors.CodeAlreadyExists(command.Code);
        }

        var code = command.Code ?? await codeGenerator.NextCodeAsync(cancellationToken);
        var handlingUnit = new HandlingUnit(new HandlingUnitCode(code), command.Type, command.LocationId);

        await context.HandlingUnits.AddAsync(handlingUnit, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return handlingUnit.Id;
    }
}
