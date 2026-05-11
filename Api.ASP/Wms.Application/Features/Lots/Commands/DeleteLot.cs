using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.Lots.Commands;

public sealed record DeleteLotCommand(Guid Id) : ICommand;

public sealed class DeleteLotValidator : AbstractValidator<DeleteLotCommand>
{
    public DeleteLotValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Lot ID is required");
    }
}

public sealed class DeleteLotCommandHandler(IAppDbContext context)
    : ICommandHandler<DeleteLotCommand>
{
    public async Task<Result> Handle(DeleteLotCommand request, CancellationToken cancellationToken)
    {
        var lot = await context.Lots
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

        if (lot is null)
            return LotErrors.NotFound(request.Id);

        lot.MarkAsDeleted();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
