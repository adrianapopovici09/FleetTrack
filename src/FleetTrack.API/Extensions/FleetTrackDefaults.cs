using FleetTrack.Application.Helpers;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace FleetTrack.API.Extensions;

public static class FleetTrackDefaults
{
    public static WebApplication MapFleetTrackDefaults(this WebApplication app)
    {
        // Health check endpoints
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = (check) => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = (check) => check.Tags.Contains(HealthCheckTags.Ready) });
        return app;
    }

    public static IServiceCollection AddFleetTrackDefaults(this IServiceCollection services)
    {
        services.AddOpenApi();
        services.AddHealthChecks();

        return services;
    }
}