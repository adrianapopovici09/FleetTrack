namespace DiLifetimes;

// The scoped service: one per request (scope). In FleetTrack the tenant comes from the token,
// so a captured TenantContext means every request runs as the first request's tenant.
// InstanceId lets a test tell instances apart; IsDisposed shows whether the scope ever released it.
public sealed class TenantContext : IDisposable
{
    public Guid InstanceId { get; } = Guid.NewGuid();

    public string? TenantId { get; set; }

    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;
}
