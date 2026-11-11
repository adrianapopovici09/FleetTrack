using FleetTrack.Application.Shipments;

using Microsoft.AspNetCore.Http.HttpResults;

namespace FleetTrack.API.Endpoints.Shipments;

public static class ShipmentEndpoints
{
    private const string GetShipmentRouteName = "GetShipment";

    public static IEndpointRouteBuilder MapShipmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shipments")
            .WithTags("Shipments");

        group.MapPost("/", CreateShipment);
        group.MapGet("/{id:guid}", GetShipment)
            .WithName(GetShipmentRouteName);
        group.MapGet("/", ListShipments);
        group.MapPatch("/{id:guid}/status", ChangeShipmentStatus);

        return app;
    }

    private static async Task<CreatedAtRoute<ShipmentResponse>> CreateShipment(
        CreateShipmentRequest request,
        ShipmentService service,
        CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request.Destination, cancellationToken);

        // Location is generated from the GET route's name, so it can't drift from the real URL.
        return TypedResults.CreatedAtRoute(
            ShipmentResponse.From(created),
            GetShipmentRouteName,
            new { id = created.Id });
    }

    private static async Task<Results<Ok<ShipmentResponse>, NotFound>> GetShipment(
        Guid id,
        ShipmentService service,
        CancellationToken cancellationToken)
    {
        var shipment = await service.GetAsync(id, cancellationToken);

        return shipment is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ShipmentResponse.From(shipment));
    }

    // Always 200: no shipments is an empty list, not a missing resource.
    private static async Task<Ok<ShipmentListResponse>> ListShipments(
        ShipmentService service,
        CancellationToken cancellationToken)
    {
        var shipments = await service.ListAsync(cancellationToken);

        return TypedResults.Ok(new ShipmentListResponse(shipments.Select(ShipmentResponse.From).ToList()));
    }

    private static async Task<Results<Ok<ShipmentResponse>, NotFound, Conflict>> ChangeShipmentStatus(
        Guid id,
        ChangeShipmentStatusRequest request,
        ShipmentService service,
        CancellationToken cancellationToken)
    {
        var change = await service.ChangeStatusAsync(id, request.Status, cancellationToken);

        // Changed and NotChanged are both success: a retried PATCH must not fail.
        return change switch
        {
            { Outcome: StatusChangeOutcome.NotFound } => TypedResults.NotFound(),
            { Outcome: StatusChangeOutcome.IllegalTransition } => TypedResults.Conflict(),
            { Shipment: { } shipment } => TypedResults.Ok(ShipmentResponse.From(shipment)),
            _ => throw new InvalidOperationException($"Unhandled status change outcome {change.Outcome}.")
        };
    }
}