using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using FleetTrack.Domain.Shipments;
using FleetTrack.IntegrationTests.Helpers;

using Testcontainers.PostgreSql;

namespace FleetTrack.IntegrationTests;

// The error contract under test: docs/api/errors.md. Type URIs are literals on purpose:
// reusing the API's ProblemTypes constants would let a changed URI pass unnoticed.
internal sealed class ShipmentEndpointsTests
{
    private const string ShipmentsUrl = "/api/v1/shipments";
    private const string ProblemJson = "application/problem+json";

    private static readonly string?[] InvalidDestinations =
    [
        null,
        "",
        "  ",
        new string('A', Shipment.DestinationMaxLength + 1),
    ];

    private static readonly string[] ValidDestinations =
    [
        "Bucharest",
        new string('A', Shipment.DestinationMaxLength),
    ];

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

    [TestCaseSource(nameof(ValidDestinations))]
    public async Task PostShipment_ThenGetLocation_ReturnsSameShipment(string destination)
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var postResponse = await client.PostAsJsonAsync(ShipmentsUrl, new { destination });
        var created = await postResponse.Content.ReadFromJsonAsync<ShipmentJson>();

        using var getResponse = await client.GetAsync(postResponse.Headers.Location);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ShipmentJson>();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(postResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(postResponse.Headers.Location?.ToString(), Does.EndWith($"{ShipmentsUrl}/{created?.Id}"));
            Assert.That(created?.Destination, Is.EqualTo(destination));
            Assert.That(created?.Status, Is.EqualTo("Pending"));
            Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(fetched, Is.EqualTo(created));
        }
    }

    [TestCaseSource(nameof(InvalidDestinations))]
    public async Task PostShipment_InvalidDestination_ReturnsValidationProblem(string? destination)
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync(ShipmentsUrl, new { destination });

        // Assert
        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "https://fleettrack.dev/errors/validation");
        Assert.That(HasError(problem, "destination"), Is.True, problem.ToString());
    }

    // The T1 bug: a missing property binds to null, which is not the same request as "destination": null.
    [Test]
    public async Task PostShipment_MissingDestinationProperty_ReturnsValidationProblem()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync(ShipmentsUrl, new { });

        // Assert
        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "https://fleettrack.dev/errors/validation");
        Assert.That(HasError(problem, "destination"), Is.True, problem.ToString());
    }

    [Test]
    public async Task GetShipment_UnknownId_ReturnsNotFoundProblem()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var unknownId = Guid.CreateVersion7();

        // Act
        using var response = await client.GetAsync($"{ShipmentsUrl}/{unknownId}");

        // Assert
        var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound, "https://fleettrack.dev/errors/not-found");
        Assert.That(StringProperty(problem, "detail"), Does.Contain(unknownId.ToString()));
    }

    [Test]
    public async Task ChangeStatus_UnknownId_ReturnsNotFoundProblem()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.PatchAsJsonAsync($"{ShipmentsUrl}/{Guid.CreateVersion7()}/status", new { status = "InTransit" });

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "https://fleettrack.dev/errors/not-found");
    }

    [Test]
    public async Task ChangeStatus_DeliveredBackToInTransit_ReturnsInvalidTransitionProblem()
    {
        // Arrange: a shipment walked to Delivered through the API itself.
        using var client = _factory.CreateClient();
        using var created = await client.PostAsJsonAsync(ShipmentsUrl, new { destination = "Cluj" });
        var statusUrl = $"{created.Headers.Location}/status";

        using (await client.PatchAsJsonAsync(statusUrl, new { status = "InTransit" }))
        using (var toDelivered = await client.PatchAsJsonAsync(statusUrl, new { status = "Delivered" }))
        {
            Assert.That(toDelivered.StatusCode, Is.EqualTo(HttpStatusCode.OK), "arrange failed: shipment not delivered");
        }

        // Act
        using var response = await client.PatchAsJsonAsync(statusUrl, new { status = "InTransit" });

        // Assert
        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict, "https://fleettrack.dev/errors/invalid-status-transition");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(StringProperty(problem, "currentStatus"), Is.EqualTo("Delivered"));
            Assert.That(StringProperty(problem, "requestedStatus"), Is.EqualTo("InTransit"));
        }
    }

    // Framework errors (errors.md §4): no FleetTrack type, but the same media type and a traceId.
    [Test]
    public async Task GetShipment_NonGuidId_ReturnsProblemJson()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"{ShipmentsUrl}/not-a-guid");

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.NotFound, expectedType: null);
    }

    [Test]
    public async Task PostShipment_MalformedJson_ReturnsProblemJson()
    {
        // Arrange
        using var client = _factory.CreateClient();
        using var content = new StringContent("{ \"destination\": ", Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PostAsync(ShipmentsUrl, content);

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, expectedType: null);
    }

    // Status and media type first, so a wrong response fails with a readable message
    // (the body is printed) instead of a JSON parsing error.
    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string? expectedType)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus), body);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo(ProblemJson), body);

        var problem = JsonSerializer.Deserialize<JsonElement>(body);
        using (Assert.EnterMultipleScope())
        {
            if (expectedType is not null)
            {
                Assert.That(StringProperty(problem, "type"), Is.EqualTo(expectedType), body);
            }

            Assert.That(StringProperty(problem, "traceId"), Is.Not.Null.And.Not.Empty, body);
        }

        return problem;
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool HasError(JsonElement problem, string field) =>
        problem.TryGetProperty("errors", out var errors) && errors.TryGetProperty(field, out _);

    // The test's own view of the JSON contract, not the API's ShipmentResponse:
    // reusing the API type would let a renamed property pass unnoticed.
    // Status is a string to check that enums go over the wire as names.
    private sealed record ShipmentJson(Guid Id, string Destination, string Status, DateTimeOffset CreatedAt);
}