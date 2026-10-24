using Microsoft.Extensions.DependencyInjection;

namespace DiLifetimes;

// Run: dotnet test labs/M01-T5-di-lifetimes
// Each test builds its own provider with explicit options, because WebApplication.CreateBuilder
// turns ValidateScopes / ValidateOnBuild on in Development only. Never rely on that default.
public class CaptiveDependencyTests
{
    private const string CaptiveMessage = "Cannot consume scoped service";

    private static ServiceProvider BuildProvider(bool validateScopes, bool validateOnBuild, Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = validateScopes,
            ValidateOnBuild = validateOnBuild,
        });
    }

    private static void RegisterCaptive(IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddSingleton<TenantReportService>();
    }

    [Test]
    public void WithoutValidationTwoScopesShareTheCapturedInstance()
    {
        using var serviceProvider = BuildProvider(validateScopes: false, validateOnBuild: false, RegisterCaptive);

        TenantReportService serviceFromScope1;
        using (var scope1 = serviceProvider.CreateScope())
        {
            serviceFromScope1 = scope1.ServiceProvider.GetRequiredService<TenantReportService>();
        }

        using var scope2 = serviceProvider.CreateScope();
        var serviceFromScope2 = scope2.ServiceProvider.GetRequiredService<TenantReportService>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(serviceFromScope2.GetInstanceId(), Is.EqualTo(serviceFromScope1.GetInstanceId()));
            // scope1 is gone, but its TenantContext was never released: it outlived the scope it belongs to.
            Assert.That(serviceFromScope1.IsTenantContextDisposed(), Is.False);
        }
    }

    [Test]
    public void ValidateScopesFailsOnFirstResolveNotAtBuild()
    {
        // Build succeeds: if it threw, the test would fail on this line.
        using var serviceProvider = BuildProvider(validateScopes: true, validateOnBuild: false, RegisterCaptive);
        using var scope = serviceProvider.CreateScope();

        // in production this would be the first request that hits the singleton, and it would throw because it tries to capture a scoped service
        Assert.That(
            () => scope.ServiceProvider.GetRequiredService<TenantReportService>(),
            Throws.InvalidOperationException.With.Message.Contains(CaptiveMessage));
    }

    [Test]
    public void ValidateOnBuildFailsAtStartup()
    {
        // ValidateOnBuild checks every registration and reports all failures together,
        // so the same InvalidOperationException arrives wrapped in an AggregateException.
        var exception = Assert.Throws<AggregateException>(
            () => BuildProvider(validateScopes: true, validateOnBuild: true, RegisterCaptive));

        Assert.That(
            exception.InnerExceptions,
            Has.Some.TypeOf<InvalidOperationException>().And.Message.Contains(CaptiveMessage));
    }

    [Test]
    public void FixedServiceGetsANewInstancePerScope()
    {
        using var serviceProvider = BuildProvider(validateScopes: true, validateOnBuild: true, services =>
        {
            services.AddScoped<TenantContext>();
            services.AddScoped<TenantReportService>();
        });

        TenantReportService serviceFromScope1;
        using (var scope1 = serviceProvider.CreateScope())
        {
            serviceFromScope1 = scope1.ServiceProvider.GetRequiredService<TenantReportService>();
        }

        using var scope2 = serviceProvider.CreateScope();
        var serviceFromScope2 = scope2.ServiceProvider.GetRequiredService<TenantReportService>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(serviceFromScope2.GetInstanceId(), Is.Not.EqualTo(serviceFromScope1.GetInstanceId()));
            // Disposing scope1 released its TenantContext; scope2's is still alive.
            Assert.That(serviceFromScope1.IsTenantContextDisposed(), Is.True);
            Assert.That(serviceFromScope2.IsTenantContextDisposed(), Is.False);
        }
    }
}
