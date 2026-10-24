namespace DiLifetimes;

// Needs the current request's TenantContext. Registered as a singleton it captures the first
// scope's instance (the captive); the fix is to register it as scoped, so it lives in the request's scope.
// IServiceScopeFactory is the fix only where there is no request scope (a BackgroundService, a queue consumer):
// a scope created here would hold a fresh TenantContext with TenantId = null, not the request's one.
public sealed class TenantReportService
{
    private readonly TenantContext _tenantContext;

    public TenantReportService(TenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public Guid GetInstanceId()
    {
        return _tenantContext.InstanceId;
    }

    // Lets the tests check whether the scope that created the context has released it.
    public bool IsTenantContextDisposed()
    {
        return _tenantContext.IsDisposed;
    }
}
