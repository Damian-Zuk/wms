using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Entities;
using Wms.Infrastructure.Identity;

namespace Wms.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser>, IAppDbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<StockIn> StockIns => Set<StockIn>();
    public DbSet<StockInItem> StockInItems => Set<StockInItem>();
    public DbSet<StockOut> StockOuts => Set<StockOut>();
    public DbSet<StockOutItem> StockOutItems => Set<StockOutItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
