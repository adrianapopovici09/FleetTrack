namespace FleetTrack.Domain.Shipments;

public sealed class Shipment
{
    public Guid Id { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string Destination { get; private set; }
    public ShipmentStatus Status { get; private set; }

    private Shipment(DateTimeOffset createdAt, string destination)
    {
        Id = Guid.CreateVersion7();
        CreatedAt = createdAt;
        Destination = destination;
        Status = ShipmentStatus.Pending;
    }

    public static Shipment Create(DateTimeOffset createdAt, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        return new Shipment(createdAt, destination);
    }

    public ShipmentResult Transition(ShipmentStatus newStatus)
    {
        if (Status == newStatus)
        {
            return ShipmentResult.NotChanged;
        }

        if (Status == ShipmentStatus.Cancelled || Status == ShipmentStatus.Delivered)
        {
            return ShipmentResult.IllegalTransition;
        }

        var isAllowed = (Status, newStatus) switch
        {
            (ShipmentStatus.Pending, ShipmentStatus.InTransit) => true,
            (ShipmentStatus.Pending, ShipmentStatus.Cancelled) => true,
            (ShipmentStatus.InTransit, ShipmentStatus.Delivered) => true,
            _ => false
        };

        if (!isAllowed)
        {
            return ShipmentResult.IllegalTransition;
        }

        Status = newStatus;
        return ShipmentResult.Changed;
    }
}