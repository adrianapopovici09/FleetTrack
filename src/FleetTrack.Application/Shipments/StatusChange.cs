namespace FleetTrack.Application.Shipments;

public enum StatusChangeOutcome
{
    Changed,
    NotChanged,
    NotFound,
    IllegalTransition
}

// Shipment is null only when Outcome is NotFound.
public sealed record StatusChange(StatusChangeOutcome Outcome, ShipmentDetails? Shipment);