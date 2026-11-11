using System.Net;
using System.Net.Http.Json;

using FleetTrack.IntegrationTests.Helpers;

using Testcontainers.PostgreSql;

namespace FleetTrack.IntegrationTests;

internal sealed class ShipmentEndpointsTests
{
    private readonly PostgreSqlContainer _container;
    private FleetTrackApiFactory _factory = null!;

    public ShipmentEndpointsTests()
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
    public async Task PostShipment_ThenGetLocation_ReturnsSameShipment()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var postResponse = await client.PostAsJsonAsync("/api/v1/shipments", new { destination = "Bucharest" });
        var created = await postResponse.Content.ReadFromJsonAsync<ShipmentJson>();

        using var getResponse = await client.GetAsync(postResponse.Headers.Location);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ShipmentJson>();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(postResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(postResponse.Headers.Location?.ToString(), Does.EndWith($"/api/v1/shipments/{created?.Id}"));
            Assert.That(created?.Destination, Is.EqualTo("Bucharest"));
            Assert.That(created?.Status, Is.EqualTo("Pending"));
            Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(fetched, Is.EqualTo(created));
        }
    }

    // The test's own view of the JSON contract, not the API's ShipmentResponse:
    // reusing the API type would let a renamed property pass unnoticed.
    // Status is a string to check that enums go over the wire as names.
    private sealed record ShipmentJson(Guid Id, string Destination, string Status, DateTimeOffset CreatedAt);
}