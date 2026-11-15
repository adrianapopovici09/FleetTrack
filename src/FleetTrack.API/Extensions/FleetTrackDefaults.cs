using FleetTrack.API.Errors;
using FleetTrack.Application.Helpers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        // Enums go over the wire as strings ("InTransit"), in the JSON and in the OpenAPI schema.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddValidation();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                if (context.ProblemDetails is HttpValidationProblemDetails validation)
                {
                    // Only AddValidation() produces HttpValidationProblemDetails, so this is
                    // the one place every validation failure passes through.
                    validation.Type = ProblemTypes.Validation;

                    var camelCased = validation.Errors.ToDictionary(
                        pair => JsonNamingPolicy.CamelCase.ConvertName(pair.Key),
                        pair => pair.Value);

                    validation.Errors.Clear();
                    foreach (var (key, messages) in camelCased)
                    {
                        validation.Errors[key] = messages;
                    }
                }
            };
        });
        services.AddExceptionHandler<UnhandledExceptionHandler>();
        return services;
    }
}