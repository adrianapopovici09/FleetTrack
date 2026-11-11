using FleetTrack.Domain.Shipments;

namespace FleetTrack.Application.Shipments;

// What the use cases return instead of the entity: an immutable snapshot that
// can't be used to change the shipment. The API maps it to its JSON contract.
public sealed record ShipmentDetails(Guid Id, string Destination, ShipmentStatus Status, DateTimeOffset CreatedAt)
{
    public static ShipmentDetails From(Shipment shipment) =>
        new(shipment.Id, shipment.Destination, shipment.Status, shipment.CreatedAt);
}