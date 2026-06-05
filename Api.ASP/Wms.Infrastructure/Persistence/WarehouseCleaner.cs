using Microsoft.EntityFrameworkCore;

namespace Wms.Infrastructure.Data;

public static class WarehouseCleaner
{
    public static async Task ClearAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context.StockMovements.ExecuteDeleteAsync(cancellationToken);
        await context.CapacityReservations.ExecuteDeleteAsync(cancellationToken);
        await context.StockInItems.ExecuteDeleteAsync(cancellationToken);
        await context.StockInLines.ExecuteDeleteAsync(cancellationToken);
        await context.StockIns.ExecuteDeleteAsync(cancellationToken);
        await context.StockOutItems.ExecuteDeleteAsync(cancellationToken);
        await context.StockOutLines.ExecuteDeleteAsync(cancellationToken);
        await context.StockOuts.ExecuteDeleteAsync(cancellationToken);
        await context.Inventories.ExecuteDeleteAsync(cancellationToken);
        await context.ProductPreferredLocations.ExecuteDeleteAsync(cancellationToken);
        await context.Lots.ExecuteDeleteAsync(cancellationToken);
        await context.Products.ExecuteDeleteAsync(cancellationToken);
        await context.Locations.ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
