using FleetTrack.Domain.Shipments;

namespace FleetTrack.Application.Shipments;

public sealed class ShipmentService(IShipmentRepository repository, TimeProvider timeProvider)
{
    public async Task<ShipmentDetails> CreateAsync(string destination, CancellationToken cancellationToken)
    {
        var shipment = Shipment.Create(timeProvider.GetUtcNow(), destination);
        await repository.AddAsync(shipment, cancellationToken);

        return ShipmentDetails.From(shipment);
    }

    public async Task<ShipmentDetails?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var shipment = await repository.GetByIdAsync(id, cancellationToken);

        return shipment is null ? null : ShipmentDetails.From(shipment);
    }

    public async Task<IReadOnlyList<ShipmentDetails>> ListAsync(CancellationToken cancellationToken)
    {
        var shipments = await repository.ListAsync(cancellationToken);

        return shipments.Select(ShipmentDetails.From).ToList();
    }

    public async Task<StatusChange> ChangeStatusAsync(Guid id, ShipmentStatus newStatus, CancellationToken cancellationToken)
    {
        var shipment = await repository.GetByIdAsync(id, cancellationToken);
        if (shipment is null)
        {
            return new StatusChange(StatusChangeOutcome.NotFound, null);
        }

        var outcome = shipment.Transition(newStatus) switch
        {
            ShipmentResult.Changed => StatusChangeOutcome.Changed,
            ShipmentResult.NotChanged => StatusChangeOutcome.NotChanged,
            ShipmentResult.IllegalTransition => StatusChangeOutcome.IllegalTransition,
            var unknown => throw new InvalidOperationException($"Unhandled transition result {unknown}.")
        };

        // Only a real change needs saving.
        if (outcome == StatusChangeOutcome.Changed)
        {
            await repository.UpdateAsync(shipment, cancellationToken);
        }

        return new StatusChange(outcome, ShipmentDetails.From(shipment));
    }
}