using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class HealthEndpointTests(MakanAppWebApplicationFactory factory)
{
    [Fact]
    public void ApplicationCanStartInTestHost()
    {
        var hostEnvironment = factory.Services.GetRequiredService<IHostEnvironment>();

        Assert.Equal("Testing", hostEnvironment.EnvironmentName);
    }

    [Fact]
    public async Task GetHealthReturnsSuccessfulResponse()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", cancellationTokenSource.Token);
        var content = await response.Content.ReadAsStringAsync(cancellationTokenSource.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", content);
    }
}