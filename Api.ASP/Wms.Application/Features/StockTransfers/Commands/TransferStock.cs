using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockTransfers.Commands;

public sealed record TransferStockCommand(
    Guid ProductId,
    Guid SourceLocationId,
    Guid DestinationLocationId,
    Guid? LotId,
    int Quantity) : ICommand<Guid>;

public sealed class TransferStockValidator : AbstractValidator<TransferStockCommand>
{
    public TransferStockValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required");
        RuleFor(x => x.SourceLocationId).NotEmpty().WithMessage("Source location ID is required");
        RuleFor(x => x.DestinationLocationId).NotEmpty().WithMessage("Destination location ID is required");
        RuleFor(x => x.DestinationLocationId)
            .NotEqual(x => x.SourceLocationId)
            .WithMessage("Source and destination locations must be different");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
    }
}

public sealed class TransferStockCommandHandler(IAppDbContext context)
    : ICommandHandler<TransferStockCommand, Guid>
{
    public async Task<Result<Guid>> Handle(TransferStockCommand command, CancellationToken cancellationToken)
    {
        if (command.SourceLocationId == command.DestinationLocationId)
            return StockTransferErrors.SameSourceAndDestination();

        var productExists = await context.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == command.ProductId, cancellationToken);

        if (!productExists)
            return StockTransferErrors.ProductNotFound(command.ProductId);

        var locationIds = new[] { command.SourceLocationId, command.DestinationLocationId };
        var existingLocationIds = await context.Locations
            .AsNoTracking()
            .Where(l => locationIds.Contains(l.Id))
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var missingLocation = locationIds.Except(existingLocationIds).FirstOrDefault();
        if (missingLocation != default)
            return StockTransferErrors.LocationNotFound(missingLocation);

        if (command.LotId.HasValue)
        {
            var lotExists = await context.Lots
                .AsNoTracking()
                .AnyAsync(l => l.Id == command.LotId.Value, cancellationToken);

            if (!lotExists)
                return StockTransferErrors.LotNotFound(command.LotId.Value);
        }

        var sourceInventory = await context.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == command.ProductId
                    && i.LocationId == command.SourceLocationId
                    && i.LotId == command.LotId,
                cancellationToken);

        if (sourceInventory is null)
            return StockTransferErrors.SourceInventoryNotFound(
                command.ProductId,
                command.SourceLocationId,
                command.LotId);

        var destinationInventory = await context.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == command.ProductId
                    && i.LocationId == command.DestinationLocationId
                    && i.LotId == command.LotId,
                cancellationToken);

        if (destinationInventory is null)
        {
            destinationInventory = new Inventory(
                command.ProductId,
                command.DestinationLocationId,
                command.LotId);
            await context.Inventories.AddAsync(destinationInventory, cancellationToken);
        }

        var transferId = Guid.NewGuid();

        var result = sourceInventory.TransferTo(
            destinationInventory,
            new Quantity(command.Quantity),
            transferId);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await context.SaveChangesAsync(cancellationToken);

        return transferId;
    }
}
