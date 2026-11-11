using FleetTrack.Application.Shipments;

using Microsoft.Extensions.DependencyInjection;

namespace FleetTrack.Application.Extensions;

public static class ApplicationRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        // Scoped, not singleton: in M03 the repository wraps a scoped DbContext,
        // and a singleton service would capture it (M01-T5).
        services.AddScoped<ShipmentService>();

        return services;
    }
}