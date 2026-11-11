using FleetTrack.Application.Shipments;
using FleetTrack.Domain.Shipments;
using FleetTrack.Infrastructure.Shipments;

namespace FleetTrack.UnitTests.Application;

internal sealed class ShipmentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 30, 0, TimeSpan.Zero);

    private ShipmentService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new ShipmentService(new InMemoryShipmentRepository(), new FixedTimeProvider(Now));
    }

    [Test]
    public async Task CreateAsync_StampsCreatedAtFromTimeProvider()
    {
        // Act
        var created = await _service.CreateAsync("Bucharest", CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.CreatedAt, Is.EqualTo(Now));
            Assert.That(created.Status, Is.EqualTo(ShipmentStatus.Pending));
        }
    }

    [Test]
    public async Task GetAsync_AfterCreate_ReturnsSameShipment()
    {
        // Arrange
        var created = await _service.CreateAsync("Bucharest", CancellationToken.None);

        // Act
        var found = await _service.GetAsync(created.Id, CancellationToken.None);

        // Assert
        Assert.That(found, Is.EqualTo(created));
    }

    [Test]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        // Act
        var found = await _service.GetAsync(Guid.CreateVersion7(), CancellationToken.None);

        // Assert
        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task ListAsync_ReturnsAllCreatedShipments()
    {
        // Arrange
        var first = await _service.CreateAsync("Bucharest", CancellationToken.None);
        var second = await _service.CreateAsync("Iasi", CancellationToken.None);

        // Act
        var shipments = await _service.ListAsync(CancellationToken.None);

        // Assert
        Assert.That(shipments, Is.EquivalentTo(new[] { first, second }));
    }

    [Test]
    public async Task ChangeStatusAsync_LegalMove_ReturnsChangedAndStoresIt()
    {
        // Arrange
        var created = await _service.CreateAsync("Bucharest", CancellationToken.None);

        // Act
        var change = await _service.ChangeStatusAsync(created.Id, ShipmentStatus.InTransit, CancellationToken.None);
        var stored = await _service.GetAsync(created.Id, CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(change.Outcome, Is.EqualTo(StatusChangeOutcome.Changed));
            Assert.That(change.Shipment?.Status, Is.EqualTo(ShipmentStatus.InTransit));
            Assert.That(stored?.Status, Is.EqualTo(ShipmentStatus.InTransit));
        }
    }

    [Test]
    public async Task ChangeStatusAsync_SameStatus_ReturnsNotChangedWithShipment()
    {
        // Arrange
        var created = await _service.CreateAsync("Bucharest", CancellationToken.None);

        // Act
        var change = await _service.ChangeStatusAsync(created.Id, ShipmentStatus.Pending, CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(change.Outcome, Is.EqualTo(StatusChangeOutcome.NotChanged));
            Assert.That(change.Shipment, Is.EqualTo(created));
        }
    }

    [Test]
    public async Task ChangeStatusAsync_IllegalMove_ReturnsIllegalTransition()
    {
        // Arrange
        var created = await _service.CreateAsync("Bucharest", CancellationToken.None);

        // Act
        var change = await _service.ChangeStatusAsync(created.Id, ShipmentStatus.Delivered, CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(change.Outcome, Is.EqualTo(StatusChangeOutcome.IllegalTransition));
            Assert.That(change.Shipment?.Status, Is.EqualTo(ShipmentStatus.Pending));
        }
    }

    [Test]
    public async Task ChangeStatusAsync_UnknownId_ReturnsNotFound()
    {
        // Act
        var change = await _service.ChangeStatusAsync(Guid.CreateVersion7(), ShipmentStatus.InTransit, CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(change.Outcome, Is.EqualTo(StatusChangeOutcome.NotFound));
            Assert.That(change.Shipment, Is.Null);
        }
    }

    // Hand-written instead of Microsoft.Extensions.TimeProvider.Testing: a fixed
    // clock is all these tests need.
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}