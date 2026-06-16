using Microsoft.EntityFrameworkCore;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Models;
using Wms.Domain.Services;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Infrastructure.Data;

/// <summary>
/// Seeds a fully-populated demo warehouse: 100 addressed locations, a categorized
/// product catalog, baseline stock, and 14 days of simulated operation (stock-ins,
/// stock-outs, transfers and adjustments).
///
/// The simulation runs through the real domain aggregates (placements, putaway,
/// reservations, picking) against an in-memory inventory ledger, then persists the
/// final state in a single SaveChanges: documents in their end status, the stock
/// movement audit trail with backdated timestamps, inventory rows that reconcile
/// exactly with the movement history, and capacity reservations for the work orders
/// still open "today". Domain events raised along the way are cleared before saving
/// so the live event handlers do not double-book anything.
/// </summary>
public static class WarehouseSeeder
{
    // ---- Determinism ----------------------------------------------------------
    private const int RngSeed = 20260610;

    // ---- Locations ------------------------------------------------------------
    private const int StorageMaxUnits = 200;
    private const decimal StorageMaxWeightKg = 250m;
    private const decimal StorageMaxVolumeDm3 = 350m;
    private const int BlockedCount = 2;
    private const int InactiveCount = 2;
    private const int NeverStockedCount = 8;
    private const int MinEmptyLocations = 7;

    // ---- Preferred locations ----------------------------------------------------
    private const int PreferredLocationsPerProduct = 2;
    private const int MaxPreferredProductsPerLocation = 2;

    // ---- Baseline stock (already on hand before the simulated window) -----------
    private const int BaselineMaxLotsPerProduct = 3;
    private const int BaselineBucketMinUnits = 30;
    private const int BaselineBucketMaxUnits = 120;

    // ---- 14-day simulation -------------------------------------------------------
    private const int HistoryDays = 14;
    private const int WeekdayStockInsMin = 7, WeekdayStockInsMax = 10;
    private const int WeekendStockInsMin = 5, WeekendStockInsMax = 6;
    private const int WeekdayStockOutsMin = 14, WeekdayStockOutsMax = 20;
    private const int WeekendStockOutsMin = 10, WeekendStockOutsMax = 12;
    private const int StockInLineMinUnits = 20, StockInLineMaxUnits = 60;
    private const int StockOutLineMinUnits = 4, StockOutLineMaxUnits = 60;
    // Average units shipped per weekday. Outbound demand is an independent process
    // (customer orders), not a function of that day's receipts — sized so the
    // 14-day total stays below total receipts while individual days may exceed them.
    private const int OutboundDailyBaseUnits = 590;
    private const double ManualPutawayRate = 0.08;    // share of lines a user re-placed
    private const double ManualPickRate = 0.07;       // share of lines a user re-allocated
    private const int MaxPlacementsPerLine = 3;
    private const int MaxPickSourcesPerLine = 3;
    private const int KeepStockFloorUnits = 6;        // anchor buckets never drain below this
    private const int PoNumberStart = 20480;
    private const int OrderNumberStart = 10200;
    private const bool IncludeTransfersAndAdjustments = true;

    private const string SystemUser = "system";
    private static readonly string[] OfficeUsers = ["magda.s", "tomasz.b", "jan.d"];
    private static readonly string[] WarehouseWorkers = ["anna.k", "piotr.w", "marek.z", "kasia.n"];

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

    // Weight is kilograms, Volume is cubic decimetres (litres), Price is per-unit cost
    // in the warehouse's base currency. Category is the leaf category name (see
    // CategoryTree); null = uncategorized.
    private static readonly (string Sku, string Name, string Description, decimal Weight, decimal Volume, decimal Price, TemperatureZone Temperature, string? Category)[] Catalog =
    [
        // Bakery & Grains
        ("RICE-5KG", "Basmati Rice 5kg", "Long-grain white basmati rice, 5kg sack", 5.0m, 6.0m, 4.20m, TemperatureZone.Ambient, "Bakery & Grains"),
        ("PASTA-500", "Spaghetti 500g", "Durum wheat spaghetti, 500g pack", 0.5m, 1.2m, 0.45m, TemperatureZone.Ambient, "Bakery & Grains"),
        ("FLOUR-1KG", "All-Purpose Flour 1kg", "Plain wheat flour, 1kg bag", 1.0m, 1.6m, 0.55m, TemperatureZone.Ambient, "Bakery & Grains"),
        ("OATS-1KG", "Rolled Oats 1kg", "Wholegrain rolled porridge oats, 1kg bag", 1.0m, 1.8m, 0.90m, TemperatureZone.Ambient, "Bakery & Grains"),
        ("CEREAL-500", "Corn Flakes 500g", "Toasted corn flakes cereal, 500g box", 0.5m, 3.0m, 1.10m, TemperatureZone.Ambient, "Bakery & Grains"),
        ("SUGAR-1KG", "Granulated Sugar 1kg", "Fine white granulated sugar, 1kg bag", 1.0m, 1.1m, 0.70m, TemperatureZone.Ambient, "Bakery & Grains"),

        // Canned & Jarred
        ("BEANS-CAN", "Canned Black Beans 400g", "Cooked black beans in water, 400g can", 0.42m, 0.45m, 0.50m, TemperatureZone.Ambient, "Canned & Jarred"),
        ("TOMATO-CAN", "Chopped Tomatoes 400g", "Chopped tomatoes in juice, 400g can", 0.4m, 0.45m, 0.40m, TemperatureZone.Ambient, "Canned & Jarred"),
        ("TUNA-CAN", "Tuna Chunks 145g", "Tuna chunks in spring water, 145g can", 0.145m, 0.2m, 0.85m, TemperatureZone.Ambient, "Canned & Jarred"),
        ("CORN-CAN", "Sweetcorn 326g", "Crisp sweetcorn kernels, 326g can", 0.33m, 0.38m, 0.55m, TemperatureZone.Ambient, "Canned & Jarred"),
        ("SOUP-CAN", "Tomato Soup 400g", "Cream of tomato soup, 400g can", 0.4m, 0.45m, 0.60m, TemperatureZone.Ambient, "Canned & Jarred"),

        // Snacks & Confectionery
        ("CHIPS-150", "Salted Potato Chips 150g", "Lightly salted potato chips, 150g bag", 0.15m, 2.5m, 0.80m, TemperatureZone.Ambient, "Snacks & Confectionery"),
        ("CHOC-100", "Milk Chocolate Bar 100g", "Smooth milk chocolate bar, 100g", 0.1m, 0.2m, 0.65m, TemperatureZone.Ambient, "Snacks & Confectionery"),
        ("BISC-300", "Digestive Biscuits 300g", "Wheat digestive biscuits, 300g pack", 0.3m, 0.9m, 0.70m, TemperatureZone.Ambient, "Snacks & Confectionery"),
        ("NUTS-200", "Roasted Mixed Nuts 200g", "Roasted & salted mixed nuts, 200g", 0.2m, 0.4m, 1.60m, TemperatureZone.Ambient, "Snacks & Confectionery"),

        // Beverages
        ("COFFEE-250", "Ground Coffee 250g", "Medium-roast ground coffee, 250g pack", 0.25m, 0.6m, 2.80m, TemperatureZone.Ambient, "Beverages"),
        ("TEA-100", "Black Tea Bags 100ct", "Black tea, 100 tea bags", 0.25m, 1.5m, 1.40m, TemperatureZone.Ambient, "Beverages"),
        ("COLA-2L", "Cola 2L", "Carbonated cola soft drink, 2L bottle", 2.1m, 2.2m, 0.95m, TemperatureZone.Ambient, "Beverages"),
        ("WATER-6PK", "Spring Water 6x1.5L", "Still spring water, 6 x 1.5L pack", 9.1m, 9.5m, 1.80m, TemperatureZone.Ambient, "Beverages"),

        // Condiments & Sauces
        ("OIL-1L", "Sunflower Oil 1L", "Refined sunflower cooking oil, 1L bottle", 0.92m, 1.05m, 1.50m, TemperatureZone.Ambient, "Condiments & Sauces"),
        ("KETCHUP-500", "Tomato Ketchup 500ml", "Thick tomato ketchup, 500ml bottle", 0.55m, 0.55m, 0.90m, TemperatureZone.Ambient, "Condiments & Sauces"),
        ("MAYO-400", "Mayonnaise 400ml", "Creamy egg mayonnaise, 400ml jar", 0.42m, 0.45m, 1.10m, TemperatureZone.Ambient, "Condiments & Sauces"),

        // Dairy & Eggs
        ("MILK-1L", "Whole Milk 1L", "Pasteurised whole cow's milk, 1L carton", 1.03m, 1.0m, 0.75m, TemperatureZone.Chilled, "Dairy & Eggs"),
        ("YOG-500", "Greek Yogurt 500g", "Strained natural Greek yogurt, 500g tub", 0.5m, 0.5m, 1.20m, TemperatureZone.Chilled, "Dairy & Eggs"),
        ("CHEESE-200", "Cheddar Cheese 200g", "Mature cheddar cheese block, 200g", 0.2m, 0.25m, 1.70m, TemperatureZone.Chilled, "Dairy & Eggs"),
        ("BUTTER-250", "Salted Butter 250g", "Churned salted butter, 250g block", 0.25m, 0.28m, 1.30m, TemperatureZone.Chilled, "Dairy & Eggs"),
        ("EGGS-12", "Free-Range Eggs (12)", "Free-range large hen eggs, dozen", 0.75m, 1.2m, 1.60m, TemperatureZone.Chilled, "Dairy & Eggs"),

        // Meat & Seafood
        ("CHKN-FILLET", "Chicken Breast Fillet 1kg", "Fresh skinless chicken breast, 1kg", 1.0m, 1.1m, 5.50m, TemperatureZone.Chilled, "Meat & Seafood"),
        ("BEEF-MINCE", "Beef Mince 500g", "Fresh lean beef mince, 500g", 0.5m, 0.55m, 3.20m, TemperatureZone.Chilled, "Meat & Seafood"),
        ("BACON-300", "Smoked Bacon 300g", "Smoked back bacon rashers, 300g", 0.3m, 0.35m, 2.10m, TemperatureZone.Chilled, "Meat & Seafood"),
        ("SALMON-FILLET", "Salmon Fillet 400g", "Fresh Atlantic salmon fillets, 400g", 0.4m, 0.45m, 4.80m, TemperatureZone.Chilled, "Meat & Seafood"),

        // Fruit & Vegetables
        ("APPLE-1KG", "Gala Apples 1kg", "Crisp Gala eating apples, 1kg", 1.0m, 1.8m, 1.40m, TemperatureZone.Ambient, "Fruit & Vegetables"),
        ("CARROT-1KG", "Carrots 1kg", "Fresh whole carrots, 1kg bag", 1.0m, 1.6m, 0.60m, TemperatureZone.Ambient, "Fruit & Vegetables"),

        // Frozen Foods
        ("PEAS-FRZ", "Frozen Garden Peas 1kg", "Frozen petit pois garden peas, 1kg bag", 1.0m, 1.5m, 1.30m, TemperatureZone.Frozen, "Frozen Foods"),
        ("ICECR-1L", "Vanilla Ice Cream 1L", "Vanilla dairy ice cream, 1L tub", 0.55m, 1.0m, 1.80m, TemperatureZone.Frozen, "Frozen Foods"),
        ("FISH-FRZ", "Frozen Cod Fillet 500g", "Skinless frozen cod fillets, 500g", 0.5m, 0.6m, 3.40m, TemperatureZone.Frozen, "Frozen Foods"),
        ("PIZZA-FRZ", "Margherita Pizza 350g", "Frozen stone-baked margherita pizza, 350g", 0.35m, 2.0m, 1.90m, TemperatureZone.Frozen, "Frozen Foods"),
        ("FRIES-FRZ", "Frozen French Fries 1.5kg", "Straight-cut frozen french fries, 1.5kg", 1.5m, 2.5m, 1.70m, TemperatureZone.Frozen, "Frozen Foods"),
        ("BERRY-FRZ", "Frozen Mixed Berries 500g", "Frozen strawberry, blueberry & raspberry mix, 500g", 0.5m, 0.7m, 2.30m, TemperatureZone.Frozen, "Frozen Foods"),

        // Cleaning Supplies
        ("DISH-500", "Dish Soap 500ml", "Concentrated washing-up liquid, 500ml", 0.55m, 0.55m, 0.85m, TemperatureZone.Ambient, "Cleaning Supplies"),
        ("BLEACH-1L", "Thin Bleach 1L", "Household thin bleach, 1L bottle", 1.05m, 1.05m, 0.70m, TemperatureZone.Ambient, "Cleaning Supplies"),
        ("SPRAY-750", "Surface Spray 750ml", "Antibacterial surface spray, 750ml", 0.8m, 0.85m, 1.40m, TemperatureZone.Ambient, "Cleaning Supplies"),

        // Paper & Disposables
        ("TOILET-9", "Toilet Roll 9pk", "Soft 2-ply toilet tissue, 9 rolls", 1.2m, 18.0m, 3.20m, TemperatureZone.Ambient, "Paper & Disposables"),
        ("KITCHEN-2", "Kitchen Towel 2pk", "Absorbent kitchen towel, 2 rolls", 0.5m, 8.0m, 1.80m, TemperatureZone.Ambient, "Paper & Disposables"),

        // Laundry
        ("DETER-2L", "Laundry Detergent 2L", "Bio liquid laundry detergent, 2L", 2.2m, 2.3m, 4.50m, TemperatureZone.Ambient, "Laundry"),

        // Personal Care
        ("SHAMP-400", "Shampoo 400ml", "Everyday nourishing shampoo, 400ml", 0.43m, 0.5m, 1.60m, TemperatureZone.Ambient, "Personal Care"),
        ("TOOTH-100", "Toothpaste 100ml", "Fluoride whitening toothpaste, 100ml", 0.13m, 0.2m, 1.10m, TemperatureZone.Ambient, "Personal Care"),

        // Health & Wellness
        ("VITC-60", "Vitamin C 60ct", "Vitamin C 500mg tablets, 60 count", 0.1m, 0.3m, 2.40m, TemperatureZone.Ambient, "Health & Wellness"),

        // Uncategorized (demonstrates the nullable category)
        ("BATT-AA4", "AA Batteries 4pk", "Alkaline AA batteries, 4 pack", 0.1m, 0.15m, 1.90m, TemperatureZone.Ambient, null),
        ("BULB-LED9", "LED Bulb 9W", "Warm-white LED light bulb, 9W E27", 0.05m, 0.4m, 2.20m, TemperatureZone.Ambient, null)
    ];

    // Shelf life in days per leaf category. Categories not listed here (household,
    // paper, laundry, personal care, uncategorized) get lots without an expiration
    // date, which feeds the "No expiry" bucket of the inventory dashboard.
    private static readonly Dictionary<string, (int Min, int Max)> ShelfLifeByCategory = new(StringComparer.Ordinal)
    {
        ["Meat & Seafood"] = (7, 21),
        ["Dairy & Eggs"] = (14, 35),
        ["Fruit & Vegetables"] = (10, 30),
        ["Bakery & Grains"] = (90, 365),
        ["Canned & Jarred"] = (365, 730),
        ["Snacks & Confectionery"] = (60, 180),
        ["Beverages"] = (120, 365),
        ["Condiments & Sauces"] = (180, 365),
        ["Frozen Foods"] = (120, 365),
        ["Health & Wellness"] = (365, 540)
    };

    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Locations.AnyAsync(cancellationToken))
            return;

        var random = new Random(RngSeed);
        var now = DateTime.UtcNow;

        var (categories, categoryIdByName) = BuildCategories();
        var locations = BuildLocations();
        var special = ApplySpecialStates(locations, random);
        var products = BuildProducts(categoryIdByName, random);
        AssignPreferredLocations(products, locations, special, random);
        StampCatalogAudit(categories, products, locations, now, random);

        var sim = new SimState(random, products, locations, special, now);
        sim.SeedBaselineStock();
        sim.SimulateHistory();
        sim.AssignHandlingUnits();
        sim.AssertConsistent();

        var inventories = sim.MaterializeInventories();
        var reservations = BuildCapacityReservations(sim.StockIns);
        StampDocumentChildren(sim.StockIns, sim.StockOuts);

        // The simulation drove the real aggregates, so putaway/pick/cancel calls
        // raised domain events. The matching stock movements were written by the
        // simulation itself (backdated); clear the events so the live handlers
        // don't book them a second time at save.
        foreach (var stockIn in sim.StockIns)
            stockIn.ClearDomainEvents();
        foreach (var stockOut in sim.StockOuts)
            stockOut.ClearDomainEvents();

        context.ProductCategories.AddRange(categories);
        context.Locations.AddRange(locations);
        context.Products.AddRange(products.Select(p => p.Product));
        context.Lots.AddRange(sim.Lots);
        context.HandlingUnits.AddRange(sim.HandlingUnits);
        context.Inventories.AddRange(inventories);
        context.StockIns.AddRange(sim.StockIns);
        context.StockOuts.AddRange(sim.StockOuts);
        context.StockMovements.AddRange(sim.Movements);
        context.CapacityReservations.AddRange(reservations);

        // Point the palletised inventory rows at their unit. HandlingUnitId is a
        // private-set domain property with no in-place "assign" operation (real flows
        // create the row already on its unit), so the seeder sets it through the change
        // tracker — consistent with how it backdates audit fields elsewhere.
        foreach (var inventory in inventories)
            if (sim.HandlingUnitByInventoryId.TryGetValue(inventory.Id, out var handlingUnitId))
                context.Entry(inventory).Property(i => i.HandlingUnitId).CurrentValue = handlingUnitId;

        await context.SaveChangesAsync(cancellationToken);

        // The live generator draws codes from the HandlingUnitCodes sequence; advance it
        // past the seeded units (HU-000001..) so the first runtime-created unit doesn't
        // collide on the unique code index.
        if (sim.HandlingUnits.Count > 0)
            await context.Database.ExecuteSqlRawAsync(
                "SELECT setval('\"HandlingUnitCodes\"', {0})",
                [sim.HandlingUnits.Count],
                cancellationToken);
    }

    // ============================== Catalog =====================================

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

    /// <summary>
    /// Product plus the seed-only attributes the simulation needs: the leaf category
    /// (drives lot shelf life) and a demand weight (drives how often stock-outs pick
    /// it, so the "top picked products" chart shows clear leaders).
    /// </summary>
    private sealed record ProductInfo(Product Product, string? Category, double Popularity);

    private static List<ProductInfo> BuildProducts(IReadOnlyDictionary<string, Guid> categoryIdByName, Random random)
    {
        var products = Catalog
            .Select(p => (Product: new Product(
                new Sku(p.Sku),
                p.Name,
                p.Weight,
                p.Volume,
                p.Price,
                p.Description,
                p.Temperature,
                p.Category is null ? null : categoryIdByName[p.Category]), p.Category))
            .ToList();

        // Power-law demand: shuffle once, then weight by rank so a handful of
        // products clearly dominate outbound volume.
        var ranks = Enumerable.Range(0, products.Count).ToList();
        Shuffle(ranks, random);

        return products
            .Select((p, i) => new ProductInfo(p.Product, p.Category, 1.0 / Math.Pow(ranks[i] + 1, 0.7)))
            .ToList();
    }

    // ============================== Locations ===================================

    private static List<Location> BuildLocations()
    {
        // Aisle × rack × shelf grids per temperature zone, sized roughly proportional
        // to the catalog mix. The grid walk guarantees address uniqueness.
        var storagePlan = new List<(LocationAddress Address, TemperatureZone Zone)>();

        void AddZone(string zone, TemperatureZone temperature, int aisles, int racks, int shelves, int count)
        {
            var added = 0;
            for (var a = 1; a <= aisles && added < count; a++)
                for (var r = 1; r <= racks && added < count; r++)
                    for (var s = 1; s <= shelves && added < count; s++)
                    {
                        storagePlan.Add((new LocationAddress(zone, $"A{a:00}", $"R{r:00}", $"S{s}", "B1"), temperature));
                        added++;
                    }
        }

        AddZone("A", TemperatureZone.Ambient, aisles: 5, racks: 4, shelves: 3, count: 60);
        AddZone("B", TemperatureZone.Chilled, aisles: 2, racks: 4, shelves: 3, count: 22);
        AddZone("C", TemperatureZone.Frozen, aisles: 2, racks: 3, shelves: 3, count: 16);

        // Codes are numbered per type in address order (zone, aisle, rack, shelf, bin).
        var locations = storagePlan
            .OrderBy(p => p.Address)
            .Select((p, i) => new Location(
                new LocationCode($"STORAGE-{i + 1:D4}"),
                p.Address,
                LocationType.Storage,
                description: $"Zone {p.Address.Zone}, aisle {p.Address.Aisle}, rack {p.Address.Rack}, shelf {p.Address.Shelf}",
                temperatureZone: p.Zone,
                capacity: StorageMaxUnits,
                weightCapacity: StorageMaxWeightKg,
                volumeCapacity: StorageMaxVolumeDm3))
            .ToList();

        locations.Add(new Location(
            new LocationCode("RETURNS-0001"),
            new LocationAddress("R", "A01", "R01", "S1", "B1"),
            LocationType.Returns,
            description: "Customer returns staging area",
            temperatureZone: TemperatureZone.Ambient));

        locations.Add(new Location(
            new LocationCode("QUARANTINE-0001"),
            new LocationAddress("Q", "A01", "R01", "S1", "B1"),
            LocationType.Quarantine,
            description: "Quarantine hold for damaged or suspect stock",
            temperatureZone: TemperatureZone.Ambient));

        return locations;
    }

    private sealed record SpecialLocations(
        Location BlockedWithStock,
        HashSet<Guid> NeverStocked,
        Location Returns,
        Location Quarantine);

    /// <summary>
    /// Blocks/deactivates a few locations (dashboard alert tiles) and designates a
    /// set that never receives stock so the warehouse always shows empty bins. All
    /// picks come from the ambient zone, which has slots to spare — chilled/frozen
    /// keep their full preferred-location capacity.
    /// </summary>
    private static SpecialLocations ApplySpecialStates(IReadOnlyList<Location> locations, Random random)
    {
        var zoneA = locations
            .Where(l => l.Type == LocationType.Storage && l.Address.Zone == "A")
            .ToList();
        Shuffle(zoneA, random);

        var blockedWithStock = zoneA[0];
        Ensure(blockedWithStock.Block("Damaged racking - pending repair"));
        Ensure(zoneA[1].Block("Spill cleanup in progress"));

        for (var i = 0; i < InactiveCount; i++)
            Ensure(zoneA[BlockedCount + i].Deactivate());

        var neverStocked = zoneA
            .Skip(BlockedCount + InactiveCount)
            .Take(NeverStockedCount)
            .Select(l => l.Id)
            .ToHashSet();

        return new SpecialLocations(
            blockedWithStock,
            neverStocked,
            locations.Single(l => l.Type == LocationType.Returns),
            locations.Single(l => l.Type == LocationType.Quarantine));
    }

    private static void AssignPreferredLocations(
        IReadOnlyList<ProductInfo> products,
        IReadOnlyList<Location> locations,
        SpecialLocations special,
        Random random)
    {
        var eligibleByZone = locations
            .Where(l => l.Type == LocationType.Storage
                && l.IsActive
                && !l.IsBlocked
                && !special.NeverStocked.Contains(l.Id))
            .GroupBy(l => l.TemperatureZone)
            .ToDictionary(g => g.Key, g => g.ToList());

        var slotsUsed = locations.ToDictionary(l => l.Id, _ => 0);

        foreach (var info in products)
        {
            var candidates = eligibleByZone[info.Product.RequiredTemperatureZone]
                .Where(l => slotsUsed[l.Id] < MaxPreferredProductsPerLocation)
                .ToList();
            Shuffle(candidates, random);

            var picked = candidates.Take(PreferredLocationsPerProduct).ToList();
            info.Product.SetPreferredLocations(picked.Select(l => l.Id));

            foreach (var location in picked)
                slotsUsed[location.Id]++;
        }
    }

    // ============================== Audit stamping ===============================

    private static void StampCatalogAudit(
        IEnumerable<ProductCategory> categories,
        IEnumerable<ProductInfo> products,
        IEnumerable<Location> locations,
        DateTime now,
        Random random)
    {
        foreach (var category in categories)
        {
            var at = now.AddDays(-random.Next(170, 240));
            category.SetCreated(at, SystemUser);
            category.SetUpdated(at, SystemUser);
        }

        foreach (var info in products)
        {
            var at = now.AddDays(-random.Next(150, 220));
            info.Product.SetCreated(at, SystemUser);
            info.Product.SetUpdated(at, SystemUser);
        }

        foreach (var location in locations)
        {
            var at = now.AddDays(-random.Next(170, 240));
            location.SetCreated(at, SystemUser);
            // Blocked/inactive states were applied recently by a person.
            location.SetUpdated(
                location.IsBlocked || !location.IsActive ? now.AddDays(-random.Next(1, 6)) : at,
                location.IsBlocked || !location.IsActive ? Pick(OfficeUsers, random) : SystemUser);
        }
    }

    /// <summary>Doc lines and items carry their parent document's audit trail.</summary>
    private static void StampDocumentChildren(IEnumerable<StockIn> stockIns, IEnumerable<StockOut> stockOuts)
    {
        foreach (var stockIn in stockIns)
            foreach (var line in stockIn.Lines)
            {
                line.SetCreated(stockIn.CreatedAt, stockIn.CreatedBy);
                line.SetUpdated(stockIn.UpdatedAt, stockIn.UpdatedBy);
                foreach (var item in line.Items)
                {
                    item.SetCreated(stockIn.CreatedAt, stockIn.CreatedBy);
                    item.SetUpdated(stockIn.UpdatedAt, stockIn.UpdatedBy);
                }
            }

        foreach (var stockOut in stockOuts)
            foreach (var line in stockOut.Lines)
            {
                line.SetCreated(stockOut.CreatedAt, stockOut.CreatedBy);
                line.SetUpdated(stockOut.UpdatedAt, stockOut.UpdatedBy);
                foreach (var item in line.Items)
                {
                    item.SetCreated(stockOut.CreatedAt, stockOut.CreatedBy);
                    item.SetUpdated(stockOut.UpdatedAt, stockOut.UpdatedBy);
                }
            }
    }

    /// <summary>
    /// Capacity holds for the not-yet-placed units of stock-ins still in Putaway —
    /// exactly what <c>StartPutaway</c> would have left behind after the partial
    /// putaways (the placed share has already been reduced and removed).
    /// </summary>
    private static List<CapacityReservation> BuildCapacityReservations(IEnumerable<StockIn> stockIns)
    {
        var reservations = new List<CapacityReservation>();

        foreach (var stockIn in stockIns.Where(s => s.Status == StockInStatus.Putaway))
            foreach (var line in stockIn.Lines)
                foreach (var item in line.Items.Where(i => i.Remaining > 0))
                {
                    var reservation = new CapacityReservation(
                        stockIn.Id,
                        item.Id,
                        item.LocationId,
                        line.ProductId,
                        line.LotId,
                        new Quantity(item.Remaining));
                    reservation.SetCreated(stockIn.CreatedAt, stockIn.CreatedBy);
                    reservation.SetUpdated(stockIn.UpdatedAt, stockIn.UpdatedBy);
                    reservations.Add(reservation);
                }

        return reservations;
    }

    // ============================== Shared helpers ===============================

    private static void Ensure(Result result)
    {
        if (result.IsFailure)
            throw new InvalidOperationException(
                $"Seeder domain call failed: {result.Error.Code} - {result.Error.Description}");
    }

    private static void Shuffle<T>(IList<T> list, Random random)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static T Pick<T>(IReadOnlyList<T> source, Random random) => source[random.Next(source.Count)];

    private static double NextDouble(Random random, double min, double max) =>
        min + random.NextDouble() * (max - min);

    // =============================================================================
    //  In-memory simulation
    // =============================================================================

    private enum StockInRole { Completed, Draft, OpenPutaway, CancelledFromDraft, CancelledFromPutaway }

    private enum StockOutRole { Completed, Draft, OpenPicking, CancelledFromDraft, CancelledFromPicking }

    private sealed record MovementSpec(int Quantity, StockMovementType Type, StockMovementSource Source, Guid SourceId, string User);

    /// <summary>
    /// One inventory bucket (product + location + lot) of the simulation ledger.
    /// The EF entity is created eagerly because adjustments reference its id as
    /// their movement source; quantities are written into it only at materialization.
    /// </summary>
    private sealed class Bucket
    {
        public required Inventory Entity { get; init; }
        public required Product Product { get; init; }
        public required Lot Lot { get; init; }
        public required Guid LocationId { get; init; }
        public int OnHand;
        public int Reserved;
        public DateTime FirstReceivedAt;
        public DateTime LastChangedAt;
        public bool KeepStock; // anchor for transfers/adjustments: never drained below the floor
        public Guid? HandlingUnitId; // set post-simulation when this stock sits on a unit
        public int Available => OnHand - Reserved;
    }

    /// <summary>Physical + held capacity load of one location, per dimension.</summary>
    private sealed class LocationLoad
    {
        private readonly Dictionary<CapacityDimension, decimal> _physical = NewDims();
        private readonly Dictionary<CapacityDimension, decimal> _held = NewDims();

        public void AddPhysical(Product product, int qty) => Accumulate(_physical, product, qty);
        public void AddHold(Product product, int qty) => Accumulate(_held, product, qty);
        public decimal Used(CapacityDimension dimension) => _physical[dimension] + _held[dimension];
        public decimal Physical(CapacityDimension dimension) => _physical[dimension];
        public int PhysicalUnits => (int)_physical[CapacityDimension.Units];
        public bool IsUntouched => _physical.Values.All(v => v == 0) && _held.Values.All(v => v == 0);

        private static Dictionary<CapacityDimension, decimal> NewDims() => new()
        {
            [CapacityDimension.Units] = 0,
            [CapacityDimension.Weight] = 0,
            [CapacityDimension.Volume] = 0
        };

        private static void Accumulate(Dictionary<CapacityDimension, decimal> target, Product product, int qty)
        {
            var sign = qty < 0 ? -1 : 1;
            foreach (var (dimension, load) in CapacityLoadCalculator.Load(product, new Quantity(Math.Abs(qty))))
                target[dimension] += sign * load;
        }
    }

    private sealed class SimState
    {
        private readonly Random _random;
        private readonly DateTime _now;
        private readonly DateTime _today;          // UTC midnight of the seed day
        private readonly IReadOnlyList<ProductInfo> _products;
        private readonly Dictionary<Guid, Location> _locationsById;
        private readonly SpecialLocations _special;
        private readonly Dictionary<TemperatureZone, List<Location>> _eligibleByZone;
        private readonly Dictionary<Guid, List<Location>> _preferredByProduct;
        private readonly Dictionary<(Guid ProductId, Guid LocationId, Guid? LotId), Bucket> _buckets = new();
        private readonly Dictionary<Guid, LocationLoad> _loads;
        private readonly Dictionary<Guid, decimal> _fillCeiling;
        private readonly Dictionary<Guid, int> _lotSequence = new();
        private readonly Dictionary<Guid, List<Lot>> _lotsByProduct = new();
        private int _poNumber = PoNumberStart;
        private int _orderNumber = OrderNumberStart;
        private long _baselineUnits;

        public List<Lot> Lots { get; } = [];
        public List<StockIn> StockIns { get; } = [];
        public List<StockOut> StockOuts { get; } = [];
        public List<StockMovement> Movements { get; } = [];
        public List<HandlingUnit> HandlingUnits { get; } = [];

        /// <summary>Inventory-row id → the handling unit it sits on, applied at save time.</summary>
        public Dictionary<Guid, Guid> HandlingUnitByInventoryId { get; } = new();

        public SimState(
            Random random,
            IReadOnlyList<ProductInfo> products,
            IReadOnlyList<Location> locations,
            SpecialLocations special,
            DateTime now)
        {
            _random = random;
            _now = now;
            _today = now.Date;
            _products = products;
            _special = special;
            _locationsById = locations.ToDictionary(l => l.Id);
            _loads = locations.ToDictionary(l => l.Id, _ => new LocationLoad());

            // Operational headroom: bins are never planned to the exact cap on any
            // dimension, and each bin's slack differs, so location fill levels show
            // a natural spread instead of a wall of bins at exactly 100%.
            _fillCeiling = locations.ToDictionary(l => l.Id, _ => 0.86m + (decimal)random.NextDouble() * 0.14m);

            _eligibleByZone = locations
                .Where(l => l.Type == LocationType.Storage
                    && l.IsActive
                    && !l.IsBlocked
                    && !special.NeverStocked.Contains(l.Id))
                .GroupBy(l => l.TemperatureZone)
                .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Address).ToList());

            _preferredByProduct = products.ToDictionary(
                p => p.Product.Id,
                p => p.Product.PreferredLocations
                    .OrderBy(pl => pl.Sequence)
                    .Select(pl => _locationsById[pl.LocationId])
                    .ToList());
        }

        // ---------------------------- Ledger core ------------------------------

        private Bucket Apply(Product product, Guid locationId, Lot lot, int onHandDelta, int reservedDelta, DateTime at, MovementSpec? movement)
        {
            var key = (product.Id, locationId, (Guid?)lot.Id);
            if (!_buckets.TryGetValue(key, out var bucket))
            {
                bucket = new Bucket
                {
                    Entity = new Inventory(product.Id, locationId, lot.Id),
                    Product = product,
                    Lot = lot,
                    LocationId = locationId,
                    FirstReceivedAt = at
                };
                _buckets[key] = bucket;
            }

            bucket.OnHand += onHandDelta;
            bucket.Reserved += reservedDelta;
            bucket.LastChangedAt = at;

            if (bucket.OnHand < 0 || bucket.Reserved < 0 || bucket.Reserved > bucket.OnHand)
                throw new InvalidOperationException(
                    $"Seeder ledger inconsistency at {product.Sku.Value} / {_locationsById[locationId].Code.Value}: " +
                    $"OnHand={bucket.OnHand}, Reserved={bucket.Reserved}");

            if (onHandDelta != 0)
                _loads[locationId].AddPhysical(product, onHandDelta);

            if (movement is null)
            {
                if (onHandDelta > 0)
                    _baselineUnits += onHandDelta;
            }
            else
            {
                var row = new StockMovement(
                    product.Id, locationId, lot.Id, movement.Quantity, movement.Type, movement.Source, movement.SourceId);
                row.SetCreated(at, movement.User);
                row.SetUpdated(at, movement.User);
                Movements.Add(row);
            }

            return bucket;
        }

        private int RoomFor(Location location, Product product)
        {
            var perUnit = CapacityLoadCalculator.Load(product, new Quantity(1));
            var load = _loads[location.Id];

            int? fit = null;
            foreach (var dimension in location.Capacity.ConfiguredDimensions())
            {
                var per = perUnit.GetValueOrDefault(dimension);
                if (per <= 0)
                    continue;

                var remaining = location.Capacity.Limit(dimension)!.Value * _fillCeiling[location.Id]
                    - load.Used(dimension);
                var dimensionFit = remaining <= 0 ? 0 : (int)Math.Floor(remaining / per);
                fit = fit is null ? dimensionFit : Math.Min(fit.Value, dimensionFit);
            }

            return fit ?? int.MaxValue;
        }

        // ---------------------------- Lots --------------------------------------

        private Lot CreateLot(ProductInfo info, DateOnly manufacture, DateOnly? expiration, DateTime createdAt, string user)
        {
            var seq = _lotSequence.GetValueOrDefault(info.Product.Id) + 1;
            _lotSequence[info.Product.Id] = seq;

            var lot = new Lot(
                new LotNumber($"{info.Product.Sku.Value}-{manufacture:yyMMdd}-{seq}"),
                info.Product.Id,
                manufacture,
                expiration);
            lot.SetCreated(createdAt, user);
            lot.SetUpdated(createdAt, user);

            Lots.Add(lot);
            if (!_lotsByProduct.TryGetValue(info.Product.Id, out var list))
                _lotsByProduct[info.Product.Id] = list = [];
            list.Add(lot);

            return lot;
        }

        private DateOnly? ExpirationFor(ProductInfo info, DateOnly manufacture)
        {
            if (info.Category is null || !ShelfLifeByCategory.TryGetValue(info.Category, out var shelf))
                return null;

            return manufacture.AddDays(_random.Next(shelf.Min, shelf.Max + 1));
        }

        private bool IsExpired(Lot lot) =>
            lot.ExpirationDate is { } expiration && expiration < DateOnly.FromDateTime(_now);

        // ---------------------------- Baseline stock ----------------------------

        public void SeedBaselineStock()
        {
            var todayDate = DateOnly.FromDateTime(_now);
            var forcedExpired = 0;

            foreach (var info in _products)
            {
                var shelf = info.Category is not null && ShelfLifeByCategory.TryGetValue(info.Category, out var s)
                    ? s : ((int Min, int Max)?)null;
                var lotCount = _random.Next(1, BaselineMaxLotsPerProduct + 1);

                for (var i = 0; i < lotCount; i++)
                {
                    DateOnly manufacture;
                    DateOnly? expiration;

                    // A couple of short-life lots are already expired with stock on
                    // hand, feeding the "expired stock" alert and expiry chart bucket.
                    var forceExpired = forcedExpired < 3
                        && i == 0
                        && info.Category is "Meat & Seafood" or "Dairy & Eggs";
                    if (forceExpired)
                    {
                        expiration = todayDate.AddDays(-_random.Next(2, 10));
                        manufacture = expiration.Value.AddDays(-shelf!.Value.Min);
                        forcedExpired++;
                    }
                    else
                    {
                        // Keep the age comfortably within the category's shelf life so
                        // short-life products get recent lots and a spread of near-expiry
                        // stock, with only the occasional naturally-expired lot.
                        var maxAge = shelf is null ? 120 : Math.Min(120, (int)(shelf.Value.Max * 0.75));
                        manufacture = todayDate.AddDays(-_random.Next(3, Math.Max(4, maxAge)));
                        expiration = shelf is null ? null : manufacture.AddDays(_random.Next(shelf.Value.Min, shelf.Value.Max + 1));
                        if (expiration is { } e && e <= manufacture)
                            expiration = manufacture.AddDays(shelf!.Value.Min);
                    }

                    var receivedAt = manufacture
                        .AddDays(_random.Next(1, 3))
                        .ToDateTime(new TimeOnly(_random.Next(8, 17), _random.Next(0, 60)), DateTimeKind.Utc);
                    if (receivedAt > _now)
                        receivedAt = _now.AddDays(-1);

                    var lot = CreateLot(info, manufacture, expiration, receivedAt, SystemUser);

                    var targets = PickBaselineLocations(info);
                    foreach (var location in targets)
                    {
                        var room = RoomFor(location, info.Product);
                        var qty = Math.Min(_random.Next(BaselineBucketMinUnits, BaselineBucketMaxUnits + 1), room);
                        if (qty < 5)
                            continue;

                        Apply(info.Product, location.Id, lot, qty, 0, receivedAt, movement: null);
                    }
                }
            }

            SeedBlockedLocationStock();
            SeedQuarantineAndReturnsStock();
            TagKeepStockAnchors();
        }

        private List<Location> PickBaselineLocations(ProductInfo info)
        {
            // Stock tends to sit in the product's preferred bins; overflow goes
            // wherever there is room in the right temperature zone.
            var preferred = _preferredByProduct[info.Product.Id]
                .Where(l => RoomFor(l, info.Product) >= BaselineBucketMinUnits)
                .ToList();
            var others = _eligibleByZone[info.Product.RequiredTemperatureZone]
                .Where(l => !preferred.Contains(l) && RoomFor(l, info.Product) >= BaselineBucketMinUnits)
                .ToList();
            Shuffle(others, _random);

            return preferred.Concat(others).Take(_random.Next(1, 3)).ToList();
        }

        /// <summary>The blocked-with-stock location got its goods before the incident.</summary>
        private void SeedBlockedLocationStock()
        {
            var ambient = _products.Where(p => p.Product.RequiredTemperatureZone == TemperatureZone.Ambient).ToList();
            for (var i = 0; i < 2; i++)
            {
                var info = Pick(ambient, _random);
                var lot = _lotsByProduct[info.Product.Id][0];
                var qty = Math.Min(_random.Next(30, 81), RoomFor(_special.BlockedWithStock, info.Product));
                if (qty < 5)
                    continue;

                Apply(info.Product, _special.BlockedWithStock.Id, lot, qty, 0, lot.CreatedAt, movement: null);
            }
        }

        /// <summary>A little stock sits in quarantine/returns so those slices render.</summary>
        private void SeedQuarantineAndReturnsStock()
        {
            var ambient = _products.Where(p => p.Product.RequiredTemperatureZone == TemperatureZone.Ambient).ToList();
            var targets = new[] { _special.Quarantine, _special.Quarantine, _special.Returns };

            foreach (var target in targets)
            {
                var info = Pick(ambient, _random);
                var lot = _lotsByProduct[info.Product.Id][0];
                var at = _now.AddDays(-_random.Next(4, 11));
                if (at < lot.CreatedAt)
                    at = lot.CreatedAt.AddHours(6); // can't arrive before the lot existed
                Apply(info.Product, target.Id, lot, _random.Next(8, 21), 0, at, movement: null);
            }
        }

        private void TagKeepStockAnchors()
        {
            var anchors = _buckets.Values
                .Where(b => b.Product.RequiredTemperatureZone == TemperatureZone.Ambient
                    && IsPickableLocation(b.LocationId)
                    && !IsExpired(b.Lot))
                .OrderByDescending(b => b.OnHand)
                .Take(12);

            foreach (var bucket in anchors)
                bucket.KeepStock = true;
        }

        // ---------------------------- 14-day history ----------------------------

        public void SimulateHistory()
        {
            for (var dayIndex = 0; dayIndex < HistoryDays; dayIndex++)
            {
                var day = _today.AddDays(-(HistoryDays - 1 - dayIndex));
                var isToday = dayIndex == HistoryDays - 1;
                var isWeekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                var trend = 0.90 + 0.02 * dayIndex; // business is mildly picking up

                var stockInCount = isWeekend
                    ? _random.Next(WeekendStockInsMin, WeekendStockInsMax + 1)
                    : _random.Next(WeekdayStockInsMin, WeekdayStockInsMax + 1);
                var stockOutCount = isWeekend
                    ? _random.Next(WeekendStockOutsMin, WeekendStockOutsMax + 1)
                    : _random.Next(WeekdayStockOutsMin, WeekdayStockOutsMax + 1);

                var stockInTimes = DocCreationTimes(day, 6.0, 13.0, stockInCount, isToday);
                var stockOutTimes = DocCreationTimes(day, 6.0, 15.0, stockOutCount, isToday);

                // Receiving follows the suppliers' delivery rhythm; line sizes carry it.
                var stockInRoles = BuildStockInRoles(stockInTimes.Count, dayIndex, isToday);
                var supplyFactor = SupplyFactorFor(day.DayOfWeek);
                for (var i = 0; i < stockInTimes.Count; i++)
                    CreateStockIn(stockInTimes[i], trend * supplyFactor, stockInRoles[i], day, isToday);

                // Shipping follows customer demand — its own weekly shape and noise,
                // decoupled from receipts, so on some days more leaves than arrives.
                var targetOutUnits = (int)(OutboundDailyBaseUnits
                    * DemandFactorFor(day.DayOfWeek)
                    * trend
                    * NextDouble(_random, 0.75, 1.30));

                var stockOutRoles = BuildStockOutRoles(stockOutTimes.Count, dayIndex, isToday);
                for (var i = 0; i < stockOutTimes.Count; i++)
                {
                    var docTarget = Math.Max(StockOutLineMinUnits, targetOutUnits / Math.Max(1, stockOutTimes.Count - i));
                    var shipped = CreateStockOut(stockOutTimes[i], docTarget, stockOutRoles[i], day, isToday);
                    targetOutUnits = Math.Max(0, targetOutUnits - shipped);
                }

                if (IncludeTransfersAndAdjustments)
                {
                    if (dayIndex is 2 or 5 or 9 or 12)
                        CreateTransfer(day);
                    if (dayIndex is 6 or 11 or 13)
                        CreateAdjustment(day, isToday);
                }
            }
        }

        /// <summary>
        /// Suppliers deliver heaviest right after the weekend and wind down toward
        /// Friday; weekends run a skeleton receiving crew.
        /// </summary>
        private static double SupplyFactorFor(DayOfWeek day) => day switch
        {
            DayOfWeek.Monday => 1.20,
            DayOfWeek.Tuesday => 1.15,
            DayOfWeek.Wednesday => 1.00,
            DayOfWeek.Thursday => 0.95,
            DayOfWeek.Friday => 0.75,
            _ => 0.55
        };

        /// <summary>
        /// Customer orders peak on Monday (weekend backlog) and before the weekend,
        /// and drop on the weekend itself — a different shape than receiving.
        /// </summary>
        private static double DemandFactorFor(DayOfWeek day) => day switch
        {
            DayOfWeek.Monday => 1.15,
            DayOfWeek.Tuesday => 0.95,
            DayOfWeek.Wednesday => 1.00,
            DayOfWeek.Thursday => 1.10,
            DayOfWeek.Friday => 1.25,
            DayOfWeek.Saturday => 0.55,
            _ => 0.45
        };

        /// <summary>
        /// Random creation times within working hours, ascending. On the seed day the
        /// window is clamped to "a moment ago" so nothing is stamped in the future;
        /// seeding right after midnight simply yields fewer (or no) documents today.
        /// </summary>
        private List<DateTime> DocCreationTimes(DateTime day, double fromHour, double toHour, int count, bool isToday)
        {
            var start = day.AddHours(fromHour);
            var end = day.AddHours(toHour);

            if (isToday)
            {
                var limit = _now.AddMinutes(-15);
                if (end > limit)
                    end = limit;
                if (end <= start)
                {
                    start = day.AddMinutes(5);
                    if (end <= start)
                        return [];
                }
            }

            var minutes = Math.Max(1, (int)(end - start).TotalMinutes);
            return Enumerable.Range(0, count)
                .Select(_ => start.AddMinutes(_random.Next(0, minutes)))
                .OrderBy(t => t)
                .ToList();
        }

        private StockInRole[] BuildStockInRoles(int count, int dayIndex, bool isToday)
        {
            var roles = new StockInRole[count]; // default: Completed

            if (count == 0)
                return roles;

            if (dayIndex == 4)
                roles[count / 2] = StockInRole.CancelledFromDraft;
            if (dayIndex == 7)
                roles[count / 2] = StockInRole.CancelledFromPutaway;
            if (dayIndex == HistoryDays - 2)
                roles[^1] = StockInRole.OpenPutaway; // yesterday's backlog

            if (isToday)
            {
                roles[^1] = StockInRole.Draft;
                if (count >= 2)
                    roles[^2] = StockInRole.OpenPutaway;
            }

            return roles;
        }

        private StockOutRole[] BuildStockOutRoles(int count, int dayIndex, bool isToday)
        {
            var roles = new StockOutRole[count]; // default: Completed

            if (count == 0)
                return roles;

            if (dayIndex is 3 or 10)
                roles[count / 2] = StockOutRole.CancelledFromDraft;
            if (dayIndex == 8)
                roles[count / 3] = StockOutRole.CancelledFromPicking;
            if (dayIndex == HistoryDays - 2)
                roles[^1] = StockOutRole.OpenPicking; // yesterday's backlog

            if (isToday)
            {
                roles[^1] = StockOutRole.Draft;
                if (count >= 2) roles[^2] = StockOutRole.Draft;
                if (count >= 3) roles[^3] = StockOutRole.OpenPicking;
                if (count >= 4) roles[^4] = StockOutRole.OpenPicking;
            }

            return roles;
        }

        // ---------------------------- Stock-ins ---------------------------------

        /// <summary>Creates one stock-in and returns the units actually received (movements written today).</summary>
        private int CreateStockIn(DateTime createdAt, double volumeFactor, StockInRole role, DateTime day, bool isToday)
        {
            var stockIn = new StockIn(Guid.NewGuid());
            stockIn.SetDescription($"PO #{_poNumber++}");
            var officeUser = Pick(OfficeUsers, _random);
            var worker = Pick(WarehouseWorkers, _random);

            var lineProducts = PickDistinctProducts(_random.Next(1, 5));
            var plannedLines = new List<(ProductInfo Info, Lot Lot, List<PlacementAllocation> Placements)>();

            foreach (var info in lineProducts)
            {
                var requested = Math.Max(StockInLineMinUnits / 2,
                    (int)(_random.Next(StockInLineMinUnits, StockInLineMaxUnits + 1) * volumeFactor));

                var placements = PlanPlacements(info, requested);
                if (placements.Count == 0)
                    continue;

                // Receipts usually arrive as a fresh lot; sometimes they top up a
                // recent one (which keeps the bucket's FIFO age, like the real flow).
                var manufacture = DateOnly.FromDateTime(day).AddDays(-_random.Next(0, 4));
                var reusable = _lotsByProduct.GetValueOrDefault(info.Product.Id)?
                    .Where(l => !IsExpired(l))
                    .ToList();
                var lot = _random.NextDouble() < 0.8 || reusable is null or { Count: 0 }
                    ? CreateLot(info, manufacture, ExpirationFor(info, manufacture), createdAt, officeUser)
                    : reusable[^1];

                plannedLines.Add((info, lot, placements));
            }

            if (plannedLines.Count == 0)
                return 0; // warehouse too full for this PO; skip it

            foreach (var (info, lot, placements) in plannedLines)
                Ensure(stockIn.AddLineWithPlacements(
                    info.Product.Id,
                    lot.Id,
                    new Quantity(placements.Sum(p => p.Quantity)),
                    placements));

            StockIns.Add(stockIn);
            stockIn.SetCreated(createdAt, officeUser);

            // Movements may not be stamped later than "now" on the seed day.
            var latest = isToday ? _now.AddMinutes(-5) : day.AddHours(21);

            switch (role)
            {
                case StockInRole.Draft:
                    stockIn.SetUpdated(createdAt, officeUser);
                    return 0;

                case StockInRole.CancelledFromDraft:
                {
                    Ensure(stockIn.Cancel());
                    ReleaseAllHolds(stockIn);
                    stockIn.SetUpdated(ClampTime(createdAt.AddHours(NextDouble(_random, 1, 4)), latest), officeUser);
                    return 0;
                }

                case StockInRole.CancelledFromPutaway:
                {
                    Ensure(stockIn.StartPutaway());
                    var cancelAt = ClampTime(createdAt.AddHours(NextDouble(_random, 4, 7)), latest);
                    var (placed, _) = PutawayShare(stockIn, createdAt, cancelAt, 0.4, 0.6, worker);

                    Ensure(stockIn.Cancel());
                    foreach (var line in stockIn.Lines)
                        foreach (var item in line.Items.Where(i => i.PlacedQuantity.Value > 0))
                        {
                            var info = FindProduct(line.ProductId);
                            var lot = FindLot(line.LotId!.Value);
                            Apply(info.Product, item.LocationId, lot, -item.PlacedQuantity.Value, 0, cancelAt,
                                new MovementSpec(item.PlacedQuantity.Value, StockMovementType.Out,
                                    StockMovementSource.StockInCancellation, stockIn.Id, worker));
                        }
                    ReleaseRemainingHolds(stockIn);
                    stockIn.SetUpdated(cancelAt, worker);
                    return placed;
                }

                case StockInRole.OpenPutaway:
                {
                    Ensure(stockIn.StartPutaway());
                    var (placed, lastAt) = PutawayShare(stockIn, createdAt, latest, 0.6, 0.8, worker);
                    stockIn.SetUpdated(lastAt, worker);
                    return placed;
                }

                default: // Completed
                {
                    var finish = createdAt.AddHours(NextDouble(_random, 2, 8));
                    if (finish > latest)
                    {
                        // Not enough of the day left — the order is still being worked.
                        Ensure(stockIn.StartPutaway());
                        var (placed, lastAt) = PutawayShare(stockIn, createdAt, latest, 0.6, 0.8, worker);
                        stockIn.SetUpdated(lastAt, worker);
                        return placed;
                    }

                    Ensure(stockIn.StartPutaway());
                    var received = 0;
                    foreach (var line in stockIn.Lines)
                    {
                        var info = FindProduct(line.ProductId);
                        var lot = FindLot(line.LotId!.Value);
                        foreach (var item in line.Items)
                        {
                            var at = BetweenTimes(createdAt, finish);
                            Ensure(stockIn.PutawayItem(item.Id, item.Quantity));
                            _loads[item.LocationId].AddHold(info.Product, -item.Quantity.Value);
                            Apply(info.Product, item.LocationId, lot, item.Quantity.Value, 0, at,
                                new MovementSpec(item.Quantity.Value, StockMovementType.In,
                                    StockMovementSource.StockIn, stockIn.Id, worker));
                            received += item.Quantity.Value;
                        }
                    }

                    Ensure(stockIn.Complete());
                    stockIn.SetUpdated(finish, worker);
                    return received;
                }
            }
        }

        /// <summary>Puts away a share of every placement, leaving the document incomplete.</summary>
        private (int Placed, DateTime LastAt) PutawayShare(
            StockIn stockIn, DateTime from, DateTime to, double minShare, double maxShare, string worker)
        {
            var placed = 0;
            var lastAt = from;
            foreach (var line in stockIn.Lines)
            {
                var info = FindProduct(line.ProductId);
                var lot = FindLot(line.LotId!.Value);
                foreach (var item in line.Items)
                {
                    var qty = (int)(item.Quantity.Value * NextDouble(_random, minShare, maxShare));
                    qty = Math.Min(qty, item.Quantity.Value - 1); // never fully place: the doc stays open
                    if (qty <= 0)
                        continue;

                    var at = BetweenTimes(from, to);
                    if (at > lastAt)
                        lastAt = at;
                    Ensure(stockIn.PutawayItem(item.Id, new Quantity(qty)));
                    _loads[item.LocationId].AddHold(info.Product, -qty);
                    Apply(info.Product, item.LocationId, lot, qty, 0, at,
                        new MovementSpec(qty, StockMovementType.In, StockMovementSource.StockIn, stockIn.Id, worker));
                    placed += qty;
                }
            }

            return (placed, lastAt);
        }

        private void ReleaseAllHolds(StockIn stockIn)
        {
            foreach (var line in stockIn.Lines)
            {
                var info = FindProduct(line.ProductId);
                foreach (var item in line.Items)
                    _loads[item.LocationId].AddHold(info.Product, -item.Quantity.Value);
            }
        }

        private void ReleaseRemainingHolds(StockIn stockIn)
        {
            foreach (var line in stockIn.Lines)
            {
                var info = FindProduct(line.ProductId);
                foreach (var item in line.Items.Where(i => i.Remaining > 0))
                    _loads[item.LocationId].AddHold(info.Product, -item.Remaining);
            }
        }

        /// <summary>
        /// Plans where a receipt line goes, mirroring the real planner's spirit:
        /// preferred bins first, then consolidation onto existing stock of the same
        /// SKU, then empty bins, then anywhere with room. Capacity holds are taken
        /// immediately so parallel plans cannot oversubscribe a bin. A small share
        /// of lines is stamped Manual, as if a user had overridden the suggestion.
        /// </summary>
        private List<PlacementAllocation> PlanPlacements(ProductInfo info, int requested)
        {
            var placements = new List<PlacementAllocation>();
            var used = new HashSet<Guid>();
            var remaining = requested;
            var manual = _random.NextDouble() < ManualPutawayRate;

            void TakeFrom(Location location, PutawayStrategyType strategy)
            {
                if (remaining <= 0 || placements.Count >= MaxPlacementsPerLine || !used.Add(location.Id))
                    return;

                var fit = Math.Min(remaining, RoomFor(location, info.Product));
                if (fit <= 0 || (fit < 4 && fit < remaining))
                {
                    used.Remove(location.Id);
                    return;
                }

                placements.Add(new PlacementAllocation(location.Id, fit, manual ? PutawayStrategyType.Manual : strategy));
                _loads[location.Id].AddHold(info.Product, fit);
                remaining -= fit;
            }

            var zone = _eligibleByZone[info.Product.RequiredTemperatureZone];

            // Occasionally the planner skips the preferred bin (e.g. it consolidates
            // instead), which keeps the strategy mix varied.
            if (_random.NextDouble() >= 0.2)
                foreach (var location in _preferredByProduct[info.Product.Id])
                    TakeFrom(location, PutawayStrategyType.PreferredLocation);

            foreach (var location in LocationsHoldingSku(info.Product, zone))
                TakeFrom(location, PutawayStrategyType.ConsolidateSameSku);

            foreach (var location in zone.Where(l => _loads[l.Id].IsUntouched))
                TakeFrom(location, PutawayStrategyType.NearestEmpty);

            foreach (var location in zone.OrderByDescending(l => RoomFor(l, info.Product)))
                TakeFrom(location, PutawayStrategyType.NearestAvailable);

            return placements;
        }

        private IEnumerable<Location> LocationsHoldingSku(Product product, IReadOnlyList<Location> zone)
        {
            var holding = _buckets.Values
                .Where(b => b.Product.Id == product.Id && b.OnHand > 0)
                .Select(b => b.LocationId)
                .ToHashSet();

            return zone
                .Where(l => holding.Contains(l.Id))
                .OrderByDescending(l => RoomFor(l, product));
        }

        // ---------------------------- Stock-outs --------------------------------

        /// <summary>Creates one stock-out and returns the units actually shipped today.</summary>
        private int CreateStockOut(DateTime createdAt, int targetUnits, StockOutRole role, DateTime day, bool isToday)
        {
            var stockOut = new StockOut(Guid.NewGuid());
            stockOut.SetDescription($"ORDER #{_orderNumber++}");
            var officeUser = Pick(OfficeUsers, _random);
            var worker = Pick(WarehouseWorkers, _random);

            var lineCount = _random.NextDouble() switch { < 0.45 => 1, < 0.80 => 2, _ => 3 };
            var perLine = Math.Max(StockOutLineMinUnits, targetUnits / lineCount);
            var chosen = new HashSet<Guid>();

            foreach (var _ in Enumerable.Range(0, lineCount))
            {
                var info = PickProductByPopularity(chosen);
                if (info is null)
                    break;
                chosen.Add(info.Product.Id);

                var strategy = PickStrategyFor(info.Product);
                var requested = Math.Clamp(
                    (int)(perLine * NextDouble(_random, 0.7, 1.3)),
                    StockOutLineMinUnits,
                    StockOutLineMaxUnits);

                var allocations = PlanPick(info, requested, strategy, createdAt);
                if (allocations.Count == 0)
                    continue;

                Ensure(stockOut.AddLineWithAllocations(
                    info.Product.Id,
                    strategy,
                    new Quantity(allocations.Sum(a => a.Quantity)),
                    allocations));
            }

            if (stockOut.Lines.Count == 0)
                return 0;

            StockOuts.Add(stockOut);
            stockOut.SetCreated(createdAt, officeUser);

            var latest = isToday ? _now.AddMinutes(-5) : day.AddHours(20);

            switch (role)
            {
                case StockOutRole.Draft:
                    stockOut.SetUpdated(createdAt, officeUser);
                    return 0;

                case StockOutRole.CancelledFromDraft:
                {
                    var cancelAt = ClampTime(createdAt.AddHours(NextDouble(_random, 0.5, 3)), latest);
                    ReleaseReservations(stockOut, cancelAt, remainderOnly: false);
                    Ensure(stockOut.Cancel());
                    stockOut.SetUpdated(cancelAt, officeUser);
                    return 0;
                }

                case StockOutRole.CancelledFromPicking:
                {
                    Ensure(stockOut.StartPicking());
                    var cancelAt = ClampTime(createdAt.AddHours(NextDouble(_random, 3, 6)), latest);
                    var (picked, _) = PickShare(stockOut, createdAt, cancelAt, 0.3, 0.5, worker);

                    Ensure(stockOut.Cancel());
                    foreach (var line in stockOut.Lines)
                        foreach (var item in line.Items.Where(i => i.PickedQuantity.Value > 0))
                        {
                            var info = FindProduct(line.ProductId);
                            var lot = FindLot(item.LotId!.Value);
                            Apply(info.Product, item.LocationId, lot, item.PickedQuantity.Value, 0, cancelAt,
                                new MovementSpec(item.PickedQuantity.Value, StockMovementType.In,
                                    StockMovementSource.StockOutCancellation, stockOut.Id, worker));
                        }
                    ReleaseReservations(stockOut, cancelAt, remainderOnly: true);
                    stockOut.SetUpdated(cancelAt, worker);
                    return picked;
                }

                case StockOutRole.OpenPicking:
                {
                    Ensure(stockOut.StartPicking());
                    var (picked, lastAt) = PickShare(stockOut, createdAt, latest, 0.4, 0.75, worker);
                    stockOut.SetUpdated(lastAt, worker);
                    return picked;
                }

                default: // Completed
                {
                    var finish = createdAt.AddHours(NextDouble(_random, 0.5, 4));
                    if (finish > latest)
                    {
                        Ensure(stockOut.StartPicking());
                        var (picked, lastAt) = PickShare(stockOut, createdAt, latest, 0.4, 0.75, worker);
                        stockOut.SetUpdated(lastAt, worker);
                        return picked;
                    }

                    Ensure(stockOut.StartPicking());
                    var shipped = 0;
                    foreach (var line in stockOut.Lines)
                    {
                        var info = FindProduct(line.ProductId);
                        foreach (var item in line.Items)
                        {
                            var lot = FindLot(item.LotId!.Value);
                            var at = BetweenTimes(createdAt, finish);
                            Ensure(stockOut.PickItem(item.Id, item.Quantity));
                            Apply(info.Product, item.LocationId, lot, -item.Quantity.Value, -item.Quantity.Value, at,
                                new MovementSpec(item.Quantity.Value, StockMovementType.Out,
                                    StockMovementSource.StockOut, stockOut.Id, worker));
                            shipped += item.Quantity.Value;
                        }
                    }

                    Ensure(stockOut.Complete());
                    stockOut.SetUpdated(finish, worker);
                    return shipped;
                }
            }
        }

        /// <summary>Picks a share of every allocation, leaving the document incomplete.</summary>
        private (int Picked, DateTime LastAt) PickShare(
            StockOut stockOut, DateTime from, DateTime to, double minShare, double maxShare, string worker)
        {
            var picked = 0;
            var lastAt = from;
            foreach (var line in stockOut.Lines)
            {
                var info = FindProduct(line.ProductId);
                foreach (var item in line.Items)
                {
                    var qty = (int)(item.Quantity.Value * NextDouble(_random, minShare, maxShare));
                    qty = Math.Min(qty, item.Quantity.Value - 1); // never fully pick: the doc stays open
                    if (qty <= 0)
                        continue;

                    var lot = FindLot(item.LotId!.Value);
                    var at = BetweenTimes(from, to);
                    if (at > lastAt)
                        lastAt = at;
                    Ensure(stockOut.PickItem(item.Id, new Quantity(qty)));
                    Apply(info.Product, item.LocationId, lot, -qty, -qty, at,
                        new MovementSpec(qty, StockMovementType.Out, StockMovementSource.StockOut, stockOut.Id, worker));
                    picked += qty;
                }
            }

            return (picked, lastAt);
        }

        /// <summary>Releases outstanding reservations (all of them, or just the unpicked remainder).</summary>
        private void ReleaseReservations(StockOut stockOut, DateTime at, bool remainderOnly)
        {
            foreach (var line in stockOut.Lines)
            {
                var info = FindProduct(line.ProductId);
                foreach (var item in line.Items)
                {
                    var outstanding = remainderOnly ? item.Remaining : item.Quantity.Value;
                    if (outstanding <= 0)
                        continue;

                    var lot = FindLot(item.LotId!.Value);
                    Apply(info.Product, item.LocationId, lot, 0, -outstanding, at, movement: null);
                }
            }
        }

        private PickingStrategyType PickStrategyFor(Product product)
        {
            if (_random.NextDouble() < ManualPickRate)
                return PickingStrategyType.Manual;

            if (product.RequiredTemperatureZone is TemperatureZone.Chilled or TemperatureZone.Frozen)
                return _random.NextDouble() < 0.75 ? PickingStrategyType.Fefo : PickingStrategyType.Fifo;

            return _random.NextDouble() switch
            {
                < 0.45 => PickingStrategyType.Fifo,
                < 0.70 => PickingStrategyType.LeastQuantity,
                < 0.88 => PickingStrategyType.Lifo,
                _ => PickingStrategyType.Fefo
            };
        }

        /// <summary>
        /// Allocates a pick across inventory buckets ordered by the strategy's
        /// semantics, reserving as it goes (mirrors CreateStockOut + the planner).
        /// </summary>
        private List<PickAllocation> PlanPick(ProductInfo info, int requested, PickingStrategyType strategy, DateTime at)
        {
            var candidates = _buckets.Values
                .Where(b => b.Product.Id == info.Product.Id
                    && IsPickableLocation(b.LocationId)
                    && !IsExpired(b.Lot)
                    && PickableUnits(b) > 0)
                .ToList();

            candidates = strategy switch
            {
                PickingStrategyType.Fefo => candidates
                    .OrderBy(b => b.Lot.ExpirationDate ?? DateOnly.MaxValue)
                    .ThenBy(b => b.FirstReceivedAt)
                    .ToList(),
                PickingStrategyType.Fifo => candidates.OrderBy(b => b.FirstReceivedAt).ToList(),
                PickingStrategyType.Lifo => candidates.OrderByDescending(b => b.FirstReceivedAt).ToList(),
                PickingStrategyType.LeastQuantity => candidates.OrderBy(b => b.Available).ToList(),
                _ => ShuffledCopy(candidates)
            };

            var allocations = new List<PickAllocation>();
            var remaining = requested;

            foreach (var bucket in candidates)
            {
                if (remaining <= 0 || allocations.Count >= MaxPickSourcesPerLine)
                    break;

                var take = Math.Min(remaining, PickableUnits(bucket));
                if (take <= 0)
                    continue;

                allocations.Add(new PickAllocation(bucket.LocationId, bucket.Lot.Id, take, strategy));
                Apply(bucket.Product, bucket.LocationId, bucket.Lot, 0, take, at, movement: null);
                remaining -= take;
            }

            return allocations;
        }

        private int PickableUnits(Bucket bucket) =>
            bucket.Available - (bucket.KeepStock ? KeepStockFloorUnits : 0);

        private bool IsPickableLocation(Guid locationId)
        {
            var location = _locationsById[locationId];
            return location.Type == LocationType.Storage && location.IsActive && !location.IsBlocked;
        }

        /// <summary>Distinct products for a receipt, weighted so popular SKUs are replenished more often.</summary>
        private List<ProductInfo> PickDistinctProducts(int count)
        {
            var picked = new List<ProductInfo>(count);
            var exclude = new HashSet<Guid>();

            for (var i = 0; i < count; i++)
            {
                var candidates = _products.Where(p => !exclude.Contains(p.Product.Id)).ToList();
                if (candidates.Count == 0)
                    break;

                var total = candidates.Sum(p => p.Popularity) + candidates.Count * 0.05;
                var roll = _random.NextDouble() * total;
                var chosen = candidates[^1];
                foreach (var candidate in candidates)
                {
                    roll -= candidate.Popularity + 0.05;
                    if (roll <= 0)
                    {
                        chosen = candidate;
                        break;
                    }
                }

                picked.Add(chosen);
                exclude.Add(chosen.Product.Id);
            }

            return picked;
        }

        private ProductInfo? PickProductByPopularity(HashSet<Guid> exclude)
        {
            var candidates = _products
                .Where(p => !exclude.Contains(p.Product.Id))
                .Where(p => _buckets.Values.Any(b => b.Product.Id == p.Product.Id
                    && IsPickableLocation(b.LocationId)
                    && !IsExpired(b.Lot)
                    && PickableUnits(b) >= StockOutLineMinUnits))
                .ToList();

            if (candidates.Count == 0)
                return null;

            var total = candidates.Sum(p => p.Popularity);
            var roll = _random.NextDouble() * total;
            foreach (var candidate in candidates)
            {
                roll -= candidate.Popularity;
                if (roll <= 0)
                    return candidate;
            }

            return candidates[^1];
        }

        // ---------------------------- Transfers & adjustments -------------------

        /// <summary>Moves a few damaged/returned units from storage into quarantine or returns.</summary>
        private void CreateTransfer(DateTime day)
        {
            var source = _buckets.Values
                .Where(b => b.KeepStock && PickableUnits(b) >= 8)
                .OrderByDescending(b => b.Available)
                .FirstOrDefault();
            if (source is null)
                return;

            var destination = _random.Next(2) == 0 ? _special.Quarantine : _special.Returns;
            var qty = Math.Min(_random.Next(5, 16), PickableUnits(source));
            var at = day.AddHours(NextDouble(_random, 10, 16));
            if (at > _now)
                at = _now.AddMinutes(-30);
            var worker = Pick(WarehouseWorkers, _random);
            var transferId = Guid.NewGuid();

            Apply(source.Product, source.LocationId, source.Lot, -qty, 0, at,
                new MovementSpec(qty, StockMovementType.Out, StockMovementSource.Transfer, transferId, worker));
            Apply(source.Product, destination.Id, source.Lot, qty, 0, at,
                new MovementSpec(qty, StockMovementType.In, StockMovementSource.Transfer, transferId, worker));
        }

        /// <summary>Cycle-count correction on a random anchor bucket.</summary>
        private void CreateAdjustment(DateTime day, bool isToday)
        {
            var anchors = _buckets.Values.Where(b => b.KeepStock && PickableUnits(b) >= 8).ToList();
            if (anchors.Count == 0)
                return;

            var bucket = Pick(anchors, _random);
            var magnitude = _random.Next(2, 9);
            var goUp = _random.Next(2) == 0;

            // Found stock still has to fit in the bin; write-offs can't drain the anchor.
            var delta = goUp
                ? Math.Min(magnitude, RoomFor(_locationsById[bucket.LocationId], bucket.Product))
                : -Math.Min(magnitude, PickableUnits(bucket) - 1);
            if (delta <= 0 && goUp)
                delta = -Math.Min(magnitude, PickableUnits(bucket) - 1);
            if (delta == 0)
                return;

            var at = day.AddHours(NextDouble(_random, 9, 17));
            if (isToday && at > _now)
                at = _now.AddMinutes(-20);
            var worker = Pick(WarehouseWorkers, _random);

            Apply(bucket.Product, bucket.LocationId, bucket.Lot, delta, 0, at,
                new MovementSpec(Math.Abs(delta), delta > 0 ? StockMovementType.In : StockMovementType.Out,
                    StockMovementSource.Adjustment, bucket.Entity.Id, worker));
        }

        // ---------------------------- Handling units ----------------------------

        /// <summary>
        /// Puts at least half of the final on-hand stock onto handling units — single-SKU
        /// pallets, boxes and containers standing at the bucket's location — mirroring how
        /// a real warehouse stores palletised goods. Runs after the simulation so it can
        /// target a precise share of the end state. The live code generator draws from the
        /// same HU-###### sequence, which <see cref="SeedAsync"/> advances past these units
        /// so runtime codes never collide.
        /// </summary>
        public void AssignHandlingUnits()
        {
            var onHand = _buckets.Values.Where(b => b.OnHand > 0).ToList();
            var totalUnits = onHand.Sum(b => (long)b.OnHand);
            if (totalUnits == 0)
                return;

            // Aim a little above half so the promise holds with margin while still
            // leaving a realistic amount of loose stock on the floor.
            var target = (long)Math.Ceiling(totalUnits * 0.58);

            long palletised = 0;
            var seq = 0;
            foreach (var bucket in ShuffledCopy(onHand))
            {
                if (palletised >= target)
                    break;

                var unit = new HandlingUnit(
                    new HandlingUnitCode($"HU-{++seq:D6}"),
                    PickHandlingUnitType(),
                    bucket.LocationId);
                unit.SetCreated(bucket.FirstReceivedAt, SystemUser);
                unit.SetUpdated(bucket.LastChangedAt, SystemUser);

                bucket.HandlingUnitId = unit.Id;
                HandlingUnits.Add(unit);
                HandlingUnitByInventoryId[bucket.Entity.Id] = unit.Id;
                palletised += bucket.OnHand;
            }
        }

        // Mostly pallets, with a scattering of boxes and containers for variety.
        private HandlingUnitType PickHandlingUnitType()
        {
            var roll = _random.NextDouble();
            return roll < 0.70 ? HandlingUnitType.Pallet
                : roll < 0.90 ? HandlingUnitType.Box
                : HandlingUnitType.Container;
        }

        // ---------------------------- Materialization ---------------------------

        public List<Inventory> MaterializeInventories()
        {
            var inventories = new List<Inventory>();

            foreach (var bucket in _buckets.Values.Where(b => b.OnHand > 0))
            {
                bucket.Entity.Receive(new Quantity(bucket.OnHand), bucket.FirstReceivedAt);
                if (bucket.Reserved > 0)
                    Ensure(bucket.Entity.Reserve(new Quantity(bucket.Reserved)));

                bucket.Entity.SetCreated(bucket.FirstReceivedAt, SystemUser);
                bucket.Entity.SetUpdated(bucket.LastChangedAt, SystemUser);
                inventories.Add(bucket.Entity);
            }

            return inventories;
        }

        // ---------------------------- Consistency checks ------------------------

        public void AssertConsistent()
        {
            // The movement ledger must reconcile exactly with the final stock.
            var inUnits = Movements.Where(m => m.Type == StockMovementType.In).Sum(m => (long)m.QuantityChange);
            var outUnits = Movements.Where(m => m.Type == StockMovementType.Out).Sum(m => (long)m.QuantityChange);
            var onHand = _buckets.Values.Sum(b => (long)b.OnHand);
            if (_baselineUnits + inUnits - outUnits != onHand)
                throw new InvalidOperationException(
                    $"Seeder ledger does not reconcile: baseline {_baselineUnits} + in {inUnits} - out {outUnits} != on-hand {onHand}");

            // Stock-ins must outweigh stock-outs over the window.
            var received = Movements.Where(m => m.Source == StockMovementSource.StockIn).Sum(m => (long)m.QuantityChange);
            var shipped = Movements.Where(m => m.Source == StockMovementSource.StockOut).Sum(m => (long)m.QuantityChange);
            if (received <= shipped)
                throw new InvalidOperationException($"Seeder shipped more than it received ({shipped} vs {received})");

            // At least half the on-hand stock must sit on handling units.
            var palletisedOnHand = _buckets.Values
                .Where(b => b.OnHand > 0 && b.HandlingUnitId is not null)
                .Sum(b => (long)b.OnHand);
            if (palletisedOnHand * 2 < onHand)
                throw new InvalidOperationException(
                    $"Less than half of on-hand stock is on handling units ({palletisedOnHand} of {onHand})");

            // Designated-empty locations stayed empty; the empty-bin promise holds.
            if (_buckets.Values.Any(b => _special.NeverStocked.Contains(b.LocationId)))
                throw new InvalidOperationException("Seeder placed stock into a designated-empty location");

            var occupied = _buckets.Values.Where(b => b.OnHand > 0).Select(b => b.LocationId).ToHashSet();
            var empty = _locationsById.Count - occupied.Count;
            if (empty < MinEmptyLocations)
                throw new InvalidOperationException($"Only {empty} locations are empty (need >= {MinEmptyLocations})");

            // Physical stock never exceeds any location's capacity.
            foreach (var (locationId, load) in _loads)
            {
                var location = _locationsById[locationId];
                foreach (var dimension in location.Capacity.ConfiguredDimensions())
                    if (load.Physical(dimension) > location.Capacity.Limit(dimension)!.Value + 0.001m)
                        throw new InvalidOperationException(
                            $"Seeder overfilled {location.Code.Value} on {dimension}");
            }
        }

        // ---------------------------- Small helpers -----------------------------

        private ProductInfo FindProduct(Guid productId) => _products.First(p => p.Product.Id == productId);

        private Lot FindLot(Guid lotId) => Lots.First(l => l.Id == lotId);

        private DateTime BetweenTimes(DateTime from, DateTime to)
        {
            if (to <= from)
                return from;
            return from.AddMinutes(_random.NextDouble() * (to - from).TotalMinutes);
        }

        private static DateTime ClampTime(DateTime value, DateTime latest) => value > latest ? latest : value;

        private List<Bucket> ShuffledCopy(List<Bucket> source)
        {
            var copy = new List<Bucket>(source);
            Shuffle(copy, _random);
            return copy;
        }
    }
}
