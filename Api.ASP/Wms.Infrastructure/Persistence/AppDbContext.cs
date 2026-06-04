using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Events;
using Wms.Domain.Entities;
using Wms.Domain.Primitives;
using Wms.Infrastructure.Identity;

namespace Wms.Infrastructure.Data;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IDomainEventDispatcher domainEventDispatcher)
    : IdentityDbContext<AppUser>(options), IAppDbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPreferredLocation> ProductPreferredLocations => Set<ProductPreferredLocation>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<StockIn> StockIns => Set<StockIn>();
    public DbSet<StockInLine> StockInLines => Set<StockInLine>();
    public DbSet<StockInItem> StockInItems => Set<StockInItem>();
    public DbSet<CapacityReservation> CapacityReservations => Set<CapacityReservation>();
    public DbSet<StockOut> StockOuts => Set<StockOut>();
    public DbSet<StockOutLine> StockOutLines => Set<StockOutLine>();
    public DbSet<StockOutItem> StockOutItems => Set<StockOutItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);
        await PublishDomainEventsAsync();
        return result;
    }

    private async Task PublishDomainEventsAsync()
    {
        var domainEvents = ChangeTracker.Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                List<IDomainEvent> events = entity.DomainEvents;
                entity.ClearDomainEvents();
                return events;
            })
            .ToList();

        await domainEventDispatcher.DispatchAsync(domainEvents);
    }
}
