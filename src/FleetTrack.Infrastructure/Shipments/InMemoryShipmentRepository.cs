using System.Collections.Concurrent;

using FleetTrack.Application.Shipments;
using FleetTrack.Domain.Shipments;

namespace FleetTrack.Infrastructure.Shipments;

// Stand-in until M03 adds EF Core. Registered as a singleton, so the data lives
// as long as the process. ConcurrentDictionary makes the dictionary safe for
// concurrent requests; it does not make the Shipment objects inside it safe.
internal sealed class InMemoryShipmentRepository : IShipmentRepository
{
    private readonly ConcurrentDictionary<Guid, Shipment> _shipments = new();

    public Task AddAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        if (!_shipments.TryAdd(shipment.Id, shipment))
        {
            throw new InvalidOperationException($"Shipment {shipment.Id} already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _shipments.TryGetValue(id, out var shipment);
        return Task.FromResult(shipment);
    }

    public Task<IReadOnlyList<Shipment>> ListAsync(CancellationToken cancellationToken)
    {
        // A dictionary has no order; sort so the list is stable between calls.
        IReadOnlyList<Shipment> shipments = _shipments.Values
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToList();

        return Task.FromResult(shipments);
    }

    public Task UpdateAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        // The stored object is the same instance the caller changed, so there is
        // nothing to copy. The method exists for the EF Core version.
        _shipments[shipment.Id] = shipment;
        return Task.CompletedTask;
    }
}