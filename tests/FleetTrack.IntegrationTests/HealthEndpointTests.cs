using System.Net;

using FleetTrack.IntegrationTests.Helpers;

using Testcontainers.PostgreSql;

namespace FleetTrack.IntegrationTests;

internal sealed class HealthEndpointTests
{
    private readonly PostgreSqlContainer _container;
    private FleetTrackApiFactory _factory = null!;

    public HealthEndpointTests()
    {
        _container = new PostgreSqlBuilder("postgres:17")
           .Build();
    }

    [OneTimeSetUp]
    public async Task Setup()
    {
        await _container.StartAsync();
        _factory = new FleetTrackApiFactory(_container.GetConnectionString());
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Test]
    public async Task HealthReady_Returns200()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health/ready");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}