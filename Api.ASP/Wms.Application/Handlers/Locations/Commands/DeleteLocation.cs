using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Locations.Commands;

public sealed record DeleteLocationCommand(Guid Id) : ICommand;

public sealed class DeleteLocationValidator : AbstractValidator<DeleteLocationCommand>
{
    public DeleteLocationValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Location ID is required");
    }
}

public sealed class DeleteLocationCommandHandler(IAppDbContext context)
    : ICommandHandler<DeleteLocationCommand>
{
    public async Task<Result> Handle(DeleteLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await context.Locations
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (location is null)
            return LocationErrors.NotFound(request.Id);

        location.MarkAsDeleted();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
