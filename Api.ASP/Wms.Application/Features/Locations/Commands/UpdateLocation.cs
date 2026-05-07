using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Shared.Common;
using Wms.Domain.ValueObjects;

namespace Wms.Application.Features.Locations.Commands;

public sealed record UpdateLocationCommand(Guid Id, string Code, string Description) : ICommand;

public sealed class UpdateLocationValidator : AbstractValidator<UpdateLocationCommand>
{
    public UpdateLocationValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Location ID is required");
        RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required");
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
            return Error.NotFound;

        location.Code = new LocationCode(request.Code);
        location.Description = request.Description;
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
