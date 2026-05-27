using Testcontainers.PostgreSql;
using Xunit;

namespace Wms.Tests.Common;

/// <summary>
/// Starts a single PostgreSQL container that is shared by every integration
/// test in the <see cref="PostgresCollection"/>. The container survives the
/// whole test run; individual tests own per-test database state via
/// <see cref="IntegrationTestBase"/>.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("wms_tests")
        .WithUsername("wms")
        .WithPassword("wms")
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public ValueTask InitializeAsync() => new(Container.StartAsync());

    public ValueTask DisposeAsync() => Container.DisposeAsync();
}
