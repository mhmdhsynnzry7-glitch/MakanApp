using Xunit;

namespace MakanApp.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<MakanAppWebApplicationFactory>
{
    public const string Name = "MakanApp SQL Server integration tests";
}
