using System.Net;
using System.Text.Json;

using FleetTrack.Application.Shipments;
using FleetTrack.Domain.Shipments;
using FleetTrack.IntegrationTests.Helpers;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FleetTrack.IntegrationTests;

// The 500 row of docs/api/errors.md: an unexpected exception becomes a generic Problem Details
// body with a traceId, and nothing from the exception reaches the client.
internal sealed class UnhandledExceptionTests
{
    // Looks like a real driver error on purpose: a host name, a user name and SQL,
    // the kind of detail that must never leave the server.
    private const string SecretMessage =
        "Npgsql: password authentication failed for user 'fleettrack_admin' on host db-internal-01 (SELECT * FROM shipments)";

    private WebApplicationFactory<Program> _factory = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        // No database is touched (the repository is replaced), so no container is needed:
        // the connection string only has to pass the non-empty startup validation.
        _factory = new FleetTrackApiFactory("Host=unused;Database=unused")
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.Replace(ServiceDescriptor.Singleton<IShipmentRepository, ThrowingShipmentRepository>())));
    }

    [OneTimeTearDown]
    public async Task TearDown() => await _factory.DisposeAsync();

    [Test]
    public async Task GetShipments_WhenRepositoryThrows_ReturnsGenericProblemDetails()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/v1/shipments");
        var body = await response.Content.ReadAsStringAsync();

        // Assert: status and media type first, so a wrong response fails with a clear message
        // instead of a JSON parsing error.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError), body);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"), body);

        var problem = JsonSerializer.Deserialize<JsonElement>(body);
        using (Assert.EnterMultipleScope())
        {
            // A literal, not ProblemTypes.InternalError: the test pins the published contract.
            Assert.That(problem.TryGetProperty("type", out var type) ? type.GetString() : null,
                Is.EqualTo("https://fleettrack.dev/errors/internal-error"));
            Assert.That(problem.TryGetProperty("title", out var title) ? title.GetString() : null,
                Is.EqualTo("An unexpected error occurred."));
            Assert.That(problem.TryGetProperty("traceId", out var traceId) ? traceId.GetString() : null,
                Is.Not.Null.And.Not.Empty);

            // Nothing from the exception may leak: not the message, not a fragment of it,
            // not the exception type or a stack trace.
            Assert.That(body, Does.Not.Contain("db-internal-01"));
            Assert.That(body, Does.Not.Contain("fleettrack_admin"));
            Assert.That(body, Does.Not.Contain("SELECT"));
            Assert.That(body, Does.Not.Contain(nameof(InvalidOperationException)));
            Assert.That(body, Does.Not.Contain(" at "), "looks like a stack trace");
        }
    }

    private sealed class ThrowingShipmentRepository : IShipmentRepository
    {
        public Task AddAsync(Shipment shipment, CancellationToken cancellationToken) => throw Failure();

        public Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw Failure();

        public Task<IReadOnlyList<Shipment>> ListAsync(CancellationToken cancellationToken) => throw Failure();

        public Task UpdateAsync(Shipment shipment, CancellationToken cancellationToken) => throw Failure();

        private static InvalidOperationException Failure() => new(SecretMessage);
    }
}