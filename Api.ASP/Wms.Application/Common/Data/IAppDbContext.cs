using Microsoft.EntityFrameworkCore;
using Wms.Domain.Entities;

namespace Wms.Application.Common.Data;

public interface IAppDbContext
{
    DbSet<Product> Products { get; }
    DbSet<ProductCategory> ProductCategories { get; }
    DbSet<ProductPreferredLocation> ProductPreferredLocations { get; }
    DbSet<Location> Locations { get; }
    DbSet<Inventory> Inventories { get; }
    DbSet<HandlingUnit> HandlingUnits { get; }
    DbSet<Lot> Lots { get; }
    DbSet<StockIn> StockIns { get; }
    DbSet<StockInLine> StockInLines { get; }
    DbSet<StockInItem> StockInItems { get; }
    DbSet<CapacityReservation> CapacityReservations { get; }
    DbSet<StockOut> StockOuts { get; }
    DbSet<StockOutLine> StockOutLines { get; }
    DbSet<StockOutItem> StockOutItems { get; }
    DbSet<StockMovement> StockMovements { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
