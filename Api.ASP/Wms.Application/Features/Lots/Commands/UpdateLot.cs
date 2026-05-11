using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.Lots.Commands;

public sealed record UpdateLotCommand(
    Guid Id,
    DateTime? ManufacturedDate,
    DateTime? ExpirationDate) : ICommand;

public sealed class UpdateLotValidator : AbstractValidator<UpdateLotCommand>
{
    public UpdateLotValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Lot ID is required");
    }
}

public sealed class UpdateLotCommandHandler(IAppDbContext context)
    : ICommandHandler<UpdateLotCommand>
{
    public async Task<Result> Handle(UpdateLotCommand request, CancellationToken cancellationToken)
    {
        var lot = await context.Lots
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

        if (lot is null)
            return LotErrors.NotFound(request.Id);

        if (request.ExpirationDate.HasValue && request.ManufacturedDate.HasValue
            && request.ExpirationDate < request.ManufacturedDate)
            return LotErrors.InvalidDates;

        lot.ManufacturedDate = request.ManufacturedDate;
        lot.ExpirationDate = request.ExpirationDate;
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
