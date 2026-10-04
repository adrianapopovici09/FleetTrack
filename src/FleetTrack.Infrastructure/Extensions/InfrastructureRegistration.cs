using FleetTrack.Application.Helpers;
using FleetTrack.Infrastructure.Configurations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FleetTrack.Infrastructure.Extensions;

public static class InfrastructureRegistration
{
    private const string ConnectionStringSection = "ConnectionStrings";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddConfiguration(services, configuration);
        AddHealthChecks(services);
        return services;
    }

    private static void AddConfiguration(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseConfiguration>()
           .Bind(configuration.GetSection(ConnectionStringSection))
           .Validate(o => !String.IsNullOrWhiteSpace(o.FleetTrack), $"Connection string '{ConnectionStringSection}:{nameof(DatabaseConfiguration.FleetTrack)}' is missing or empty.\r\nLocally: dotnet user-secrets set \"{ConnectionStringSection}:{nameof(DatabaseConfiguration.FleetTrack)}\" \"<value>\" --project src/FleetTrack.API.\r\nIn containers/CI: set the environment variable {ConnectionStringSection}__{nameof(DatabaseConfiguration.FleetTrack)}.")
           .ValidateOnStart();
    }

    private static void AddHealthChecks(IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddNpgSql(
                connectionStringFactory: sp => sp.GetRequiredService<IOptions<DatabaseConfiguration>>().Value.FleetTrack,
                name: "postgres",
                tags: [HealthCheckTags.Ready],
                timeout: TimeSpan.FromSeconds(5)
            );
    }
}