using FleetTrack.Domain.Shipments;

namespace FleetTrack.Application.Shipments;

// Async with a CancellationToken even though the in-memory version completes
// synchronously: the interface is shaped for the real database (M03).
public interface IShipmentRepository
{
    Task AddAsync(Shipment shipment, CancellationToken cancellationToken);

    Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Shipment>> ListAsync(CancellationToken cancellationToken);

    Task UpdateAsync(Shipment shipment, CancellationToken cancellationToken);
}