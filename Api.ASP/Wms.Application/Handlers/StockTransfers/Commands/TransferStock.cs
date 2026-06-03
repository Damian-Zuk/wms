using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Extensions;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockTransfers.Commands;

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

        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == command.ProductId, cancellationToken);

        if (product is null)
            return StockTransferErrors.ProductNotFound(command.ProductId);

        var locationIds = new[] { command.SourceLocationId, command.DestinationLocationId };
        var locations = await context.Locations
            .Where(l => locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        if (!locations.ContainsKey(command.SourceLocationId))
            return StockTransferErrors.LocationNotFound(command.SourceLocationId);

        if (!locations.TryGetValue(command.DestinationLocationId, out var destinationLocation))
            return StockTransferErrors.LocationNotFound(command.DestinationLocationId);

        Lot? lot = null;
        if (command.LotId.HasValue)
        {
            lot = await context.Lots
                .FirstOrDefaultAsync(l => l.Id == command.LotId.Value, cancellationToken);

            if (lot is null)
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

        var destinationContents = await context.Inventories
            .Where(i => i.LocationId == command.DestinationLocationId)
            .ToListAsync(cancellationToken);

        var quantity = new Quantity(command.Quantity);

        var canAccept = destinationLocation.CanAccept(product, lot, quantity, destinationContents);
        if (canAccept.IsFailure)
            return Result.Failure<Guid>(canAccept.Error);

        var destinationInventory = destinationContents.FirstOrDefault(i =>
            i.ProductId == command.ProductId && i.LotId == command.LotId);

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
            quantity,
            transferId);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        var saveResult = await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
        if (saveResult.IsFailure)
            return Result.Failure<Guid>(saveResult.Error);

        return transferId;
    }
}
