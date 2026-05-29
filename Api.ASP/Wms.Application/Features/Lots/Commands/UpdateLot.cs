using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.Lots.Commands;

public sealed record UpdateLotCommand(
    Guid Id,
    DateOnly? ManufactureDate,
    DateOnly? ExpirationDate) : ICommand;

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

        var result = lot.UpdateDates(request.ManufactureDate, request.ExpirationDate);
        if (result.IsFailure)
            return result;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
