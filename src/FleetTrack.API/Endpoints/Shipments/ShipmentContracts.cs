using FleetTrack.Application.Shipments;
using FleetTrack.Domain.Shipments;

namespace FleetTrack.API.Endpoints.Shipments;

// The JSON contract of /api/v1/shipments. Only the API layer knows these types;
// the Application layer works with ShipmentDetails.

public sealed record CreateShipmentRequest(string Destination);

public sealed record ChangeShipmentStatusRequest(ShipmentStatus Status);

public sealed record ShipmentResponse(Guid Id, string Destination, ShipmentStatus Status, DateTimeOffset CreatedAt)
{
    public static ShipmentResponse From(ShipmentDetails details) =>
        new(details.Id, details.Destination, details.Status, details.CreatedAt);
}

// An object, not a bare array, so T3 can add a "next" link without breaking clients.
public sealed record ShipmentListResponse(IReadOnlyList<ShipmentResponse> Items);