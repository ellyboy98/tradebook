namespace TradeBook.Tests.Integration;

/// <summary>
/// Marker that binds <see cref="SqlServerFixture"/> to the "Integration"
/// collection. Test classes opt in with <c>[Collection(IntegrationCollection.Name)]</c>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Integration";
}
