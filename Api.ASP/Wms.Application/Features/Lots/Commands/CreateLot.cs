using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Entities;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Features.Lots.Commands;

public sealed record CreateLotCommand(
    string Number,
    Guid ProductId,
    DateTime? ManufacturedDate,
    DateTime? ExpirationDate) : ICommand<Guid>;

public sealed class CreateLotValidator : AbstractValidator<CreateLotCommand>
{
    public CreateLotValidator()
    {
        RuleFor(x => x.Number).NotEmpty().WithMessage("Lot number is required");
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required");
    }
}

public sealed class CreateLotCommandHandler(IAppDbContext context)
    : ICommandHandler<CreateLotCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateLotCommand request, CancellationToken cancellationToken)
    {
        var productExists = await context.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.ProductId, cancellationToken);

        if (!productExists)
            return Error.NotFound;

        var exists = await context.Lots
            .AsNoTracking()
            .AnyAsync(l => l.Number.Value == request.Number && l.ProductId == request.ProductId, cancellationToken);

        if (exists)
            return Error.Problem("Lot.NumberExists", "Lot with this number already exists for this product.");

        Lot lot;
        try
        {
            lot = new Lot(new LotNumber(request.Number), request.ProductId, request.ManufacturedDate, request.ExpirationDate);
        }
        catch (ArgumentException ex)
        {
            return Error.Problem("Lot.InvalidDates", ex.Message);
        }

        await context.Lots.AddAsync(lot, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return lot.Id;
    }
}
