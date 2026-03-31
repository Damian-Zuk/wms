using Microsoft.EntityFrameworkCore;
using Wms.Domain.Entities;

namespace Wms.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Product> Products { get; }
    DbSet<Location> Locations { get; }
    DbSet<Inventory> Inventories { get; }
    DbSet<StockMovement> StockMovements { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
