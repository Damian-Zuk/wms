using Microsoft.EntityFrameworkCore;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;

namespace Wms.Infrastructure.Data;

public static class WarehouseSeeder
{
    private static readonly (string Code, TemperatureZone Temperature)[] Zones =
    [
        ("A", TemperatureZone.Ambient),
        ("B", TemperatureZone.Chilled),
        ("C", TemperatureZone.Frozen)
    ];

    // Weight is kilograms, Volume is cubic decimetres (litres) — per unit.
    private static readonly (string Sku, string Name, string Description, decimal Weight, decimal Volume, TemperatureZone Temperature)[] Catalog =
    [
        ("RICE-5KG", "Basmati Rice 5kg", "Long-grain white basmati rice, 5kg sack", 5.0m, 6.0m, TemperatureZone.Ambient),
        ("PASTA-500", "Spaghetti 500g", "Durum wheat spaghetti, 500g pack", 0.5m, 1.2m, TemperatureZone.Ambient),
        ("FLOUR-1KG", "All-Purpose Flour 1kg", "Plain wheat flour, 1kg bag", 1.0m, 1.6m, TemperatureZone.Ambient),
        ("SUGAR-1KG", "Granulated Sugar 1kg", "Fine white granulated sugar, 1kg bag", 1.0m, 1.1m, TemperatureZone.Ambient),
        ("BEANS-CAN", "Canned Black Beans 400g", "Cooked black beans in water, 400g can", 0.42m, 0.45m, TemperatureZone.Ambient),
        ("COFFEE-250", "Ground Coffee 250g", "Medium-roast ground coffee, 250g pack", 0.25m, 0.6m, TemperatureZone.Ambient),
        ("OIL-1L", "Sunflower Oil 1L", "Refined sunflower cooking oil, 1L bottle", 0.92m, 1.05m, TemperatureZone.Ambient),
        ("MILK-1L", "Whole Milk 1L", "Pasteurised whole cow's milk, 1L carton", 1.03m, 1.0m, TemperatureZone.Chilled),
        ("YOG-500", "Greek Yogurt 500g", "Strained natural Greek yogurt, 500g tub", 0.5m, 0.5m, TemperatureZone.Chilled),
        ("CHEESE-200", "Cheddar Cheese 200g", "Mature cheddar cheese block, 200g", 0.2m, 0.25m, TemperatureZone.Chilled),
        ("BUTTER-250", "Salted Butter 250g", "Churned salted butter, 250g block", 0.25m, 0.28m, TemperatureZone.Chilled),
        ("CHKN-FILLET", "Chicken Breast Fillet 1kg", "Fresh skinless chicken breast, 1kg", 1.0m, 1.1m, TemperatureZone.Chilled),
        ("JUICE-1L", "Orange Juice 1L", "Not-from-concentrate orange juice, 1L", 1.05m, 1.0m, TemperatureZone.Chilled),
        ("EGGS-12", "Free-Range Eggs (12)", "Free-range large hen eggs, dozen", 0.75m, 1.2m, TemperatureZone.Chilled),
        ("PEAS-FRZ", "Frozen Garden Peas 1kg", "Frozen petit pois garden peas, 1kg bag", 1.0m, 1.5m, TemperatureZone.Frozen),
        ("ICECR-1L", "Vanilla Ice Cream 1L", "Vanilla dairy ice cream, 1L tub", 0.55m, 1.0m, TemperatureZone.Frozen),
        ("FISH-FILLET", "Frozen Cod Fillet 500g", "Skinless frozen cod fillets, 500g", 0.5m, 0.6m, TemperatureZone.Frozen),
        ("PIZZA-FRZ", "Margherita Pizza 350g", "Frozen stone-baked margherita pizza, 350g", 0.35m, 2.0m, TemperatureZone.Frozen),
        ("FRIES-FRZ", "Frozen French Fries 1.5kg", "Straight-cut frozen french fries, 1.5kg", 1.5m, 2.5m, TemperatureZone.Frozen),
        ("BERRY-FRZ", "Frozen Mixed Berries 500g", "Frozen strawberry, blueberry & raspberry mix, 500g", 0.5m, 0.7m, TemperatureZone.Frozen)
    ];

    private const int LocationCount = 50;
    private const int LotsPerProduct = 5;
    private const int LocationsPerLot = 2;
    private const int LocationCapacity = 2000;
    // Storage weight/volume caps, sized to comfortably exceed the seeded fill (≤ 350 units
    // of the heaviest/bulkiest product per location) so seeded occupancy never exceeds 100%.
    private const decimal StorageWeightCapacity = 2500m;
    private const decimal StorageVolumeCapacity = 4000m;
    private const double MaxFillRatio = 0.70;
    private const int PreferredLocationsPerProduct = 2;
    private const int MaxProductsPerPreferredLocation = 4;

    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Locations.AnyAsync(cancellationToken))
            return;

        var random = new Random(20260529);

        var locations = BuildLocations();
        await context.Locations.AddRangeAsync(locations, cancellationToken);

        var products = BuildProducts();
        await context.Products.AddRangeAsync(products, cancellationToken);

        var lots = BuildLots(products, random);
        await context.Lots.AddRangeAsync(lots, cancellationToken);

        var inventories = BuildInventories(products, locations, lots, random);
        await context.Inventories.AddRangeAsync(inventories, cancellationToken);

        AssignPreferredLocations(products, locations, random);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static List<Location> BuildLocations()
    {
        var locations = new List<Location>(LocationCount);
        var perZoneCount = new int[Zones.Length];

        for (var i = 0; i < LocationCount; i++)
        {
            var zoneIndex = i % Zones.Length;
            var (zoneCode, temperature) = Zones[zoneIndex];
            var n = perZoneCount[zoneIndex]++;

            var aisle = $"A{(n / 10) + 1:00}";
            var rack = $"R{(n % 10) + 1:00}";
            var address = new LocationAddress(zoneCode, aisle, rack, "S1", "B1");

            var type = i switch
            {
                0 => LocationType.Returns,
                1 => LocationType.Quarantine,
                _ => LocationType.Storage
            };

            // Storage bins cap units, weight and volume; quarantine and returns are unlimited.
            var isStorage = type == LocationType.Storage;

            locations.Add(new Location(
                new LocationCode($"LOC-{i + 1:D4}"),
                address,
                type,
                description: $"Zone {zoneCode} aisle {aisle} rack {rack}",
                temperatureZone: temperature,
                capacity: null, // isStorage ? LocationCapacity : null,
                weightCapacity: isStorage ? StorageWeightCapacity : null,
                volumeCapacity: isStorage ? StorageVolumeCapacity : null));
        }

        return locations;
    }

    private static List<Product> BuildProducts() =>
        Catalog
            .Select(p => new Product(new Sku(p.Sku), p.Name, p.Weight, p.Volume, p.Description, p.Temperature))
            .ToList();

    private static List<Lot> BuildLots(IReadOnlyList<Product> products, Random random)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var lots = new List<Lot>(products.Count * LotsPerProduct);

        foreach (var product in products)
        {
            for (var seq = 1; seq <= LotsPerProduct; seq++)
            {
                var manufacture = today.AddDays(-random.Next(5, 121));

                var shelfLifeDays = product.RequiredTemperatureZone switch
                {
                    TemperatureZone.Chilled => random.Next(14, 46),
                    TemperatureZone.Frozen => random.Next(180, 366),
                    _ => random.Next(365, 541)
                };

                var expiration = manufacture.AddDays(shelfLifeDays);
                var number = new LotNumber($"{product.Sku.Value}-{manufacture:yyMMdd}-{seq}");

                lots.Add(new Lot(number, product.Id, manufacture, expiration));
            }
        }

        return lots;
    }

    private static List<Inventory> BuildInventories(
        IReadOnlyList<Product> products,
        IReadOnlyList<Location> locations,
        IReadOnlyList<Lot> lots,
        Random random)
    {
        var productsById = products.ToDictionary(p => p.Id);
        var locationsByZone = locations
            .GroupBy(l => l.TemperatureZone)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Location>)g.ToList());

        var inventories = new List<Inventory>(lots.Count * LocationsPerLot);
        var fillBudget = (int)(LocationCapacity * MaxFillRatio);
        var remainingCapacity = locations.ToDictionary(l => l.Id, _ => fillBudget);

        foreach (var lot in lots)
        {
            var product = productsById[lot.ProductId];
            var zoneLocations = locationsByZone[product.RequiredTemperatureZone];

            foreach (var location in PickDistinct(zoneLocations, LocationsPerLot, random))
            {
                var available = remainingCapacity[location.Id];
                if (available <= 0)
                    continue;

                var onHand = Math.Min(random.Next(10, 241), available);

                // Stamp the received date from the lot's manufacture date so FIFO picking
                // has a meaningful age signal on seed data (falls back to now if unset).
                var receivedAt = lot.ManufactureDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                    ?? DateTime.UtcNow;

                var inventory = new Inventory(product.Id, location.Id, lot.Id);
                inventory.Receive(new Quantity(onHand), receivedAt);
                inventories.Add(inventory);

                remainingCapacity[location.Id] = available - onHand;
            }
        }

        return inventories;
    }

    private static void AssignPreferredLocations(
        IReadOnlyList<Product> products,
        IReadOnlyList<Location> locations,
        Random random)
    {
        var locationsByZone = locations
            .GroupBy(l => l.TemperatureZone)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Tracks how many products have claimed each location as preferred.
        var preferredCountByLocation = locations.ToDictionary(l => l.Id, _ => 0);

        foreach (var product in products)
        {
            var candidates = locationsByZone[product.RequiredTemperatureZone]
                .Where(l => preferredCountByLocation[l.Id] < MaxProductsPerPreferredLocation)
                .ToList();

            // Shuffle so each product gets a varied assignment.
            for (var i = candidates.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            var picked = candidates.Take(PreferredLocationsPerProduct).ToList();

            product.SetPreferredLocations(picked.Select(l => l.Id));

            foreach (var location in picked)
                preferredCountByLocation[location.Id]++;
        }
    }

    private static IEnumerable<Location> PickDistinct(
        IReadOnlyList<Location> source,
        int count,
        Random random)
    {
        var indices = Enumerable.Range(0, source.Count).ToList();

        for (var i = 0; i < count; i++)
        {
            var pick = random.Next(i, indices.Count);
            (indices[i], indices[pick]) = (indices[pick], indices[i]);
            yield return source[indices[i]];
        }
    }
}
