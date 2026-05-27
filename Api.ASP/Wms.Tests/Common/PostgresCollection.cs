using Xunit;

namespace Wms.Tests.Common;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = nameof(PostgresCollection);
}
