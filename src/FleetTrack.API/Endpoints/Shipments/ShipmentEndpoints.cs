using FleetTrack.API.Errors;
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

        // ProblemHttpResult carries no OpenAPI metadata (its status code is only known at run time),
        // so each error response is declared here for the generated document.
        group.MapPost("/", CreateShipment)
            .ProducesValidationProblem();
        group.MapGet("/{id:guid}", GetShipment)
            .WithName(GetShipmentRouteName)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/", ListShipments);
        group.MapPatch("/{id:guid}/status", ChangeShipmentStatus)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

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

    private static async Task<Results<Ok<ShipmentResponse>, ProblemHttpResult>> GetShipment(
        Guid id,
        ShipmentService service,
        CancellationToken cancellationToken)
    {
        var shipment = await service.GetAsync(id, cancellationToken);

        return shipment is null
            ? ShipmentNotFound(id)
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

    private static async Task<Results<Ok<ShipmentResponse>, ProblemHttpResult>> ChangeShipmentStatus(
        Guid id,
        ChangeShipmentStatusRequest request,
        ShipmentService service,
        CancellationToken cancellationToken)
    {
        var change = await service.ChangeStatusAsync(id, request.Status, cancellationToken);

        // Changed and NotChanged are both success: a retried PATCH must not fail.
        return change switch
        {
            { Outcome: StatusChangeOutcome.NotFound } => ShipmentNotFound(id),
            { Outcome: StatusChangeOutcome.IllegalTransition, Shipment: { } current } =>
                InvalidStatusTransition(id, current.Status.ToString(), request.Status.ToString()),
            { Shipment: { } shipment } => TypedResults.Ok(ShipmentResponse.From(shipment)),
            _ => throw new InvalidOperationException($"Unhandled status change outcome {change.Outcome}.")
        };
    }

    // Titles are fixed per type (docs/api/errors.md); only detail and extensions vary per occurrence.
    private static ProblemHttpResult ShipmentNotFound(Guid id) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            type: ProblemTypes.NotFound,
            title: "The resource was not found.",
            detail: $"Shipment {id} was not found.");

    private static ProblemHttpResult InvalidStatusTransition(Guid id, string currentStatus, string requestedStatus) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: ProblemTypes.InvalidStatusTransition,
            title: "The status transition is not allowed.",
            detail: $"Shipment {id} is {currentStatus} and cannot move to {requestedStatus}.",
            extensions: new Dictionary<string, object?>
            {
                ["currentStatus"] = currentStatus,
                ["requestedStatus"] = requestedStatus,
            });
}