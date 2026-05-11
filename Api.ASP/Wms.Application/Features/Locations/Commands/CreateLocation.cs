using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Entities;
using Wms.Domain.ValueObjects;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.Locations.Commands;

public sealed record CreateLocationCommand(string Code, string? Description) : ICommand<Guid>;

public sealed class CreateLocationValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required");
    }
}

public sealed class CreateLocationCommandHandler(IAppDbContext context)
    : ICommandHandler<CreateLocationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateLocationCommand request, CancellationToken cancellationToken)
    {
        var exists = await context.Locations
            .AsNoTracking()
            .AnyAsync(l => l.Code.Value == request.Code, cancellationToken);

        if (exists)
            return LocationErrors.CodeNotFound(request.Code);

        var location = new Location(new LocationCode(request.Code), request.Description);
        await context.Locations.AddAsync(location, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return location.Id;
    }
}
