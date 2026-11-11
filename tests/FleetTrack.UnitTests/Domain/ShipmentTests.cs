using FleetTrack.Domain.Shipments;

namespace FleetTrack.UnitTests.Domain;

// The rules under test: docs/design/M02-T1-shipment-api-contract.md §6.
internal sealed class ShipmentTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 11, 9, 30, 0, TimeSpan.Zero);

    [Test]
    public void Create_StartsPendingWithVersion7Id()
    {
        // Act
        var shipment = Shipment.Create(CreatedAt, "Bucharest");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(shipment.Id.Version, Is.EqualTo(7));
            Assert.That(shipment.Status, Is.EqualTo(ShipmentStatus.Pending));
            Assert.That(shipment.Destination, Is.EqualTo("Bucharest"));
            Assert.That(shipment.CreatedAt, Is.EqualTo(CreatedAt));
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Create_MissingDestination_Throws(string? destination)
    {
        // Act & Assert
        Assert.That(() => Shipment.Create(CreatedAt, destination!), Throws.InstanceOf<ArgumentException>());
    }

    [TestCase(ShipmentStatus.Pending, ShipmentStatus.InTransit)]
    [TestCase(ShipmentStatus.Pending, ShipmentStatus.Cancelled)]
    [TestCase(ShipmentStatus.InTransit, ShipmentStatus.Delivered)]
    public void Transition_LegalMove_ChangesStatus(ShipmentStatus from, ShipmentStatus to)
    {
        // Arrange
        var shipment = ShipmentIn(from);

        // Act
        var result = shipment.Transition(to);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(ShipmentResult.Changed));
            Assert.That(shipment.Status, Is.EqualTo(to));
        }
    }

    [TestCase(ShipmentStatus.Pending, ShipmentStatus.Delivered)]
    [TestCase(ShipmentStatus.InTransit, ShipmentStatus.Pending)]
    [TestCase(ShipmentStatus.InTransit, ShipmentStatus.Cancelled)]
    [TestCase(ShipmentStatus.Delivered, ShipmentStatus.Pending)]
    [TestCase(ShipmentStatus.Delivered, ShipmentStatus.InTransit)]
    [TestCase(ShipmentStatus.Delivered, ShipmentStatus.Cancelled)]
    [TestCase(ShipmentStatus.Cancelled, ShipmentStatus.Pending)]
    [TestCase(ShipmentStatus.Cancelled, ShipmentStatus.InTransit)]
    [TestCase(ShipmentStatus.Cancelled, ShipmentStatus.Delivered)]
    public void Transition_IllegalMove_KeepsStatus(ShipmentStatus from, ShipmentStatus to)
    {
        // Arrange
        var shipment = ShipmentIn(from);

        // Act
        var result = shipment.Transition(to);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(ShipmentResult.IllegalTransition));
            Assert.That(shipment.Status, Is.EqualTo(from));
        }
    }

    [TestCase(ShipmentStatus.Pending)]
    [TestCase(ShipmentStatus.InTransit)]
    [TestCase(ShipmentStatus.Delivered)]
    [TestCase(ShipmentStatus.Cancelled)]
    public void Transition_SameStatus_KeepsStatus(ShipmentStatus status)
    {
        // Arrange
        var shipment = ShipmentIn(status);

        // Act
        var result = shipment.Transition(status);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(ShipmentResult.NotChanged));
            Assert.That(shipment.Status, Is.EqualTo(status));
        }
    }

    // Reaches each status through legal moves only, so a test never relies on
    // the illegal moves it's checking.
    private static Shipment ShipmentIn(ShipmentStatus status)
    {
        var shipment = Shipment.Create(CreatedAt, "Bucharest");

        switch (status)
        {
            case ShipmentStatus.InTransit:
                shipment.Transition(ShipmentStatus.InTransit);
                break;
            case ShipmentStatus.Delivered:
                shipment.Transition(ShipmentStatus.InTransit);
                shipment.Transition(ShipmentStatus.Delivered);
                break;
            case ShipmentStatus.Cancelled:
                shipment.Transition(ShipmentStatus.Cancelled);
                break;
        }

        Assume.That(shipment.Status, Is.EqualTo(status), "test setup could not reach the starting status");
        return shipment;
    }
}