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

    // Hierarchical product-category tree. Parents are listed before their children
    // so ids can be resolved in a single pass. Category names are unique.
    private static readonly (string Name, string? Parent)[] CategoryTree =
    [
        ("Food", null),
        ("Bakery & Grains", "Food"),
        ("Canned & Jarred", "Food"),
        ("Snacks & Confectionery", "Food"),
        ("Beverages", "Food"),
        ("Condiments & Sauces", "Food"),
        ("Dairy & Eggs", "Food"),
        ("Meat & Seafood", "Food"),
        ("Fruit & Vegetables", "Food"),
        ("Frozen Foods", "Food"),
        ("Household", null),
        ("Cleaning Supplies", "Household"),
        ("Paper & Disposables", "Household"),
        ("Laundry", "Household"),
        ("Health & Beauty", null),
        ("Personal Care", "Health & Beauty"),
        ("Health & Wellness", "Health & Beauty")
    ];

    // Weight is kilograms, Volume is cubic decimetres (litres) — per unit.
    // Category is the leaf category name (see CategoryTree); null = uncategorized.
    private static readonly (string Sku, string Name, string Description, decimal Weight, decimal Volume, TemperatureZone Temperature, string? Category)[] Catalog =
    [
        // Bakery & Grains
        ("RICE-5KG", "Basmati Rice 5kg", "Long-grain white basmati rice, 5kg sack", 5.0m, 6.0m, TemperatureZone.Ambient, "Bakery & Grains"),
        ("PASTA-500", "Spaghetti 500g", "Durum wheat spaghetti, 500g pack", 0.5m, 1.2m, TemperatureZone.Ambient, "Bakery & Grains"),
        ("FLOUR-1KG", "All-Purpose Flour 1kg", "Plain wheat flour, 1kg bag", 1.0m, 1.6m, TemperatureZone.Ambient, "Bakery & Grains"),
        ("OATS-1KG", "Rolled Oats 1kg", "Wholegrain rolled porridge oats, 1kg bag", 1.0m, 1.8m, TemperatureZone.Ambient, "Bakery & Grains"),
        ("CEREAL-500", "Corn Flakes 500g", "Toasted corn flakes cereal, 500g box", 0.5m, 3.0m, TemperatureZone.Ambient, "Bakery & Grains"),
        ("SUGAR-1KG", "Granulated Sugar 1kg", "Fine white granulated sugar, 1kg bag", 1.0m, 1.1m, TemperatureZone.Ambient, "Bakery & Grains"),

        // Canned & Jarred
        ("BEANS-CAN", "Canned Black Beans 400g", "Cooked black beans in water, 400g can", 0.42m, 0.45m, TemperatureZone.Ambient, "Canned & Jarred"),
        ("TOMATO-CAN", "Chopped Tomatoes 400g", "Chopped tomatoes in juice, 400g can", 0.4m, 0.45m, TemperatureZone.Ambient, "Canned & Jarred"),
        ("TUNA-CAN", "Tuna Chunks 145g", "Tuna chunks in spring water, 145g can", 0.145m, 0.2m, TemperatureZone.Ambient, "Canned & Jarred"),
        ("CORN-CAN", "Sweetcorn 326g", "Crisp sweetcorn kernels, 326g can", 0.33m, 0.38m, TemperatureZone.Ambient, "Canned & Jarred"),
        ("SOUP-CAN", "Tomato Soup 400g", "Cream of tomato soup, 400g can", 0.4m, 0.45m, TemperatureZone.Ambient, "Canned & Jarred"),

        // Snacks & Confectionery
        ("CHIPS-150", "Salted Potato Chips 150g", "Lightly salted potato chips, 150g bag", 0.15m, 2.5m, TemperatureZone.Ambient, "Snacks & Confectionery"),
        ("CHOC-100", "Milk Chocolate Bar 100g", "Smooth milk chocolate bar, 100g", 0.1m, 0.2m, TemperatureZone.Ambient, "Snacks & Confectionery"),
        ("BISC-300", "Digestive Biscuits 300g", "Wheat digestive biscuits, 300g pack", 0.3m, 0.9m, TemperatureZone.Ambient, "Snacks & Confectionery"),
        ("NUTS-200", "Roasted Mixed Nuts 200g", "Roasted & salted mixed nuts, 200g", 0.2m, 0.4m, TemperatureZone.Ambient, "Snacks & Confectionery"),

        // Beverages
        ("COFFEE-250", "Ground Coffee 250g", "Medium-roast ground coffee, 250g pack", 0.25m, 0.6m, TemperatureZone.Ambient, "Beverages"),
        ("TEA-100", "Black Tea Bags 100ct", "Black tea, 100 tea bags", 0.25m, 1.5m, TemperatureZone.Ambient, "Beverages"),
        ("COLA-2L", "Cola 2L", "Carbonated cola soft drink, 2L bottle", 2.1m, 2.2m, TemperatureZone.Ambient, "Beverages"),
        ("WATER-6PK", "Spring Water 6x1.5L", "Still spring water, 6 x 1.5L pack", 9.1m, 9.5m, TemperatureZone.Ambient, "Beverages"),

        // Condiments & Sauces
        ("OIL-1L", "Sunflower Oil 1L", "Refined sunflower cooking oil, 1L bottle", 0.92m, 1.05m, TemperatureZone.Ambient, "Condiments & Sauces"),
        ("KETCHUP-500", "Tomato Ketchup 500ml", "Thick tomato ketchup, 500ml bottle", 0.55m, 0.55m, TemperatureZone.Ambient, "Condiments & Sauces"),
        ("MAYO-400", "Mayonnaise 400ml", "Creamy egg mayonnaise, 400ml jar", 0.42m, 0.45m, TemperatureZone.Ambient, "Condiments & Sauces"),

        // Dairy & Eggs
        ("MILK-1L", "Whole Milk 1L", "Pasteurised whole cow's milk, 1L carton", 1.03m, 1.0m, TemperatureZone.Chilled, "Dairy & Eggs"),
        ("YOG-500", "Greek Yogurt 500g", "Strained natural Greek yogurt, 500g tub", 0.5m, 0.5m, TemperatureZone.Chilled, "Dairy & Eggs"),
        ("CHEESE-200", "Cheddar Cheese 200g", "Mature cheddar cheese block, 200g", 0.2m, 0.25m, TemperatureZone.Chilled, "Dairy & Eggs"),
        ("BUTTER-250", "Salted Butter 250g", "Churned salted butter, 250g block", 0.25m, 0.28m, TemperatureZone.Chilled, "Dairy & Eggs"),
        ("EGGS-12", "Free-Range Eggs (12)", "Free-range large hen eggs, dozen", 0.75m, 1.2m, TemperatureZone.Chilled, "Dairy & Eggs"),

        // Meat & Seafood
        ("CHKN-FILLET", "Chicken Breast Fillet 1kg", "Fresh skinless chicken breast, 1kg", 1.0m, 1.1m, TemperatureZone.Chilled, "Meat & Seafood"),
        ("BEEF-MINCE", "Beef Mince 500g", "Fresh lean beef mince, 500g", 0.5m, 0.55m, TemperatureZone.Chilled, "Meat & Seafood"),
        ("BACON-300", "Smoked Bacon 300g", "Smoked back bacon rashers, 300g", 0.3m, 0.35m, TemperatureZone.Chilled, "Meat & Seafood"),
        ("SALMON-FILLET", "Salmon Fillet 400g", "Fresh Atlantic salmon fillets, 400g", 0.4m, 0.45m, TemperatureZone.Chilled, "Meat & Seafood"),

        // Fruit & Vegetables
        ("APPLE-1KG", "Gala Apples 1kg", "Crisp Gala eating apples, 1kg", 1.0m, 1.8m, TemperatureZone.Ambient, "Fruit & Vegetables"),
        ("CARROT-1KG", "Carrots 1kg", "Fresh whole carrots, 1kg bag", 1.0m, 1.6m, TemperatureZone.Ambient, "Fruit & Vegetables"),

        // Frozen Foods
        ("PEAS-FRZ", "Frozen Garden Peas 1kg", "Frozen petit pois garden peas, 1kg bag", 1.0m, 1.5m, TemperatureZone.Frozen, "Frozen Foods"),
        ("ICECR-1L", "Vanilla Ice Cream 1L", "Vanilla dairy ice cream, 1L tub", 0.55m, 1.0m, TemperatureZone.Frozen, "Frozen Foods"),
        ("FISH-FRZ", "Frozen Cod Fillet 500g", "Skinless frozen cod fillets, 500g", 0.5m, 0.6m, TemperatureZone.Frozen, "Frozen Foods"),
        ("PIZZA-FRZ", "Margherita Pizza 350g", "Frozen stone-baked margherita pizza, 350g", 0.35m, 2.0m, TemperatureZone.Frozen, "Frozen Foods"),
        ("FRIES-FRZ", "Frozen French Fries 1.5kg", "Straight-cut frozen french fries, 1.5kg", 1.5m, 2.5m, TemperatureZone.Frozen, "Frozen Foods"),
        ("BERRY-FRZ", "Frozen Mixed Berries 500g", "Frozen strawberry, blueberry & raspberry mix, 500g", 0.5m, 0.7m, TemperatureZone.Frozen, "Frozen Foods"),

        // Cleaning Supplies
        ("DISH-500", "Dish Soap 500ml", "Concentrated washing-up liquid, 500ml", 0.55m, 0.55m, TemperatureZone.Ambient, "Cleaning Supplies"),
        ("BLEACH-1L", "Thin Bleach 1L", "Household thin bleach, 1L bottle", 1.05m, 1.05m, TemperatureZone.Ambient, "Cleaning Supplies"),
        ("SPRAY-750", "Surface Spray 750ml", "Antibacterial surface spray, 750ml", 0.8m, 0.85m, TemperatureZone.Ambient, "Cleaning Supplies"),

        // Paper & Disposables
        ("TOILET-9", "Toilet Roll 9pk", "Soft 2-ply toilet tissue, 9 rolls", 1.2m, 18.0m, TemperatureZone.Ambient, "Paper & Disposables"),
        ("KITCHEN-2", "Kitchen Towel 2pk", "Absorbent kitchen towel, 2 rolls", 0.5m, 8.0m, TemperatureZone.Ambient, "Paper & Disposables"),

        // Laundry
        ("DETER-2L", "Laundry Detergent 2L", "Bio liquid laundry detergent, 2L", 2.2m, 2.3m, TemperatureZone.Ambient, "Laundry"),

        // Personal Care
        ("SHAMP-400", "Shampoo 400ml", "Everyday nourishing shampoo, 400ml", 0.43m, 0.5m, TemperatureZone.Ambient, "Personal Care"),
        ("TOOTH-100", "Toothpaste 100ml", "Fluoride whitening toothpaste, 100ml", 0.13m, 0.2m, TemperatureZone.Ambient, "Personal Care"),

        // Health & Wellness
        ("VITC-60", "Vitamin C 60ct", "Vitamin C 500mg tablets, 60 count", 0.1m, 0.3m, TemperatureZone.Ambient, "Health & Wellness"),

        // Uncategorized (demonstrates the nullable category)
        ("BATT-AA4", "AA Batteries 4pk", "Alkaline AA batteries, 4 pack", 0.1m, 0.15m, TemperatureZone.Ambient, null),
        ("BULB-LED9", "LED Bulb 9W", "Warm-white LED light bulb, 9W E27", 0.05m, 0.4m, TemperatureZone.Ambient, null)
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

        var (categories, categoryIdByName) = BuildCategories();
        await context.ProductCategories.AddRangeAsync(categories, cancellationToken);

        var products = BuildProducts(categoryIdByName);
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

    private static (List<ProductCategory> Categories, Dictionary<string, Guid> IdByName) BuildCategories()
    {
        var idByName = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var categories = new List<ProductCategory>(CategoryTree.Length);

        foreach (var (name, parent) in CategoryTree)
        {
            Guid? parentId = parent is null ? null : idByName[parent];
            var category = new ProductCategory(name, parentId);
            categories.Add(category);
            idByName[name] = category.Id;
        }

        return (categories, idByName);
    }

    private static List<Product> BuildProducts(IReadOnlyDictionary<string, Guid> categoryIdByName) =>
        Catalog
            .Select(p => new Product(
                new Sku(p.Sku),
                p.Name,
                p.Weight,
                p.Volume,
                p.Description,
                p.Temperature,
                p.Category is null ? null : categoryIdByName[p.Category]))
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
