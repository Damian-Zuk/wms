using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Features.Lots.Commands;

public sealed record CreateLotCommand(
    string Number,
    Guid ProductId,
    DateOnly? ManufactureDate,
    DateOnly? ExpirationDate) : ICommand<Guid>;

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
            return LotErrors.ProductNotFound(request.ProductId);

        var exists = await context.Lots
            .AsNoTracking()
            .AnyAsync(l => l.Number.Value == request.Number && l.ProductId == request.ProductId, cancellationToken);

        if (exists)
            return LotErrors.NumberExists(request.Number);

        LotNumber lotNumber;
        try
        {
            lotNumber = new LotNumber(request.Number);
        }
        catch (ArgumentException)
        {
            return LotErrors.EmptyNumber;
        }

        Lot lot;
        try
        {
            lot = new Lot(lotNumber, request.ProductId, request.ManufactureDate, request.ExpirationDate);
        }
        catch (ArgumentException)
        {
            return LotErrors.InvalidDates;
        }

        await context.Lots.AddAsync(lot, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return lot.Id;
    }
}
