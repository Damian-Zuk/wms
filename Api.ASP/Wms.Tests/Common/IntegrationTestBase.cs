using Microsoft.EntityFrameworkCore;
using Wms.Infrastructure.Data;
using Wms.Infrastructure.Persistence.Interceptors;
using Xunit;
using Xunit.v3;

namespace Wms.Tests.Common;

/// <summary>
/// Base class for tests that need a real PostgreSQL-backed
/// <see cref="AppDbContext"/>. Each test gets a fresh, truncated database so
/// tests do not see each other's data.
/// </summary>
[Collection(PostgresCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaCreated;

    private static readonly string[] TablesToTruncate =
    [
        "StockMovements",
        "CapacityReservations",
        "StockInItems",
        "StockInLines",
        "StockOutItems",
        "StockOutLines",
        "StockIns",
        "StockOuts",
        "Inventories",
        "HandlingUnits",
        "ProductPreferredLocations",
        "Lots",
        "Products",
        "Locations"
    ];

    private readonly PostgresContainerFixture _fixture;

    protected IntegrationTestBase(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    protected AppDbContext Context { get; private set; } = null!;

    protected TestDomainEventDispatcher EventDispatcher { get; } = new();

    protected TestCurrentUserService CurrentUser { get; } = new();

    public async ValueTask InitializeAsync()
    {
        Context = CreateContext();
        await EnsureSchemaCreatedAsync();
        await ResetDatabaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
    }

    /// <summary>
    /// Creates a fresh <see cref="AppDbContext"/> against the shared
    /// container. Test handlers that should not see entities tracked by the
    /// arrange-phase context can resolve a clean context this way.
    /// </summary>
    protected AppDbContext CreateContext()
    {
        var auditInterceptor = new AuditInterceptor(CurrentUser);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .AddInterceptors(auditInterceptor)
            .Options;

        return new AppDbContext(options, EventDispatcher);
    }

    private async Task EnsureSchemaCreatedAsync()
    {
        if (_schemaCreated) return;

        await SchemaLock.WaitAsync();
        try
        {
            if (_schemaCreated) return;
            await Context.Database.EnsureCreatedAsync();
            _schemaCreated = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private async Task ResetDatabaseAsync()
    {
        var sql = "TRUNCATE TABLE " +
            string.Join(", ", TablesToTruncate.Select(t => $"\"{t}\"")) +
            " RESTART IDENTITY CASCADE;";

        await Context.Database.ExecuteSqlRawAsync(sql);
    }
}
