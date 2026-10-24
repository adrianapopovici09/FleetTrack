using Microsoft.Extensions.DependencyInjection;

namespace DiLifetimes;

// Run: dotnet test labs/M01-T5-di-lifetimes
// Each test builds its own provider with explicit options, because WebApplication.CreateBuilder
// turns ValidateScopes / ValidateOnBuild on in Development only. Never rely on that default.
public class CaptiveDependencyTests
{
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

    [Test]
    public void WithoutValidationTwoScopesShareTheCapturedInstance()
    {
        // TODO (you): register the captive singleton + scoped TenantContext, resolve the singleton
        // from two different scopes, and assert both see the same TenantContext (and it's never disposed).
        Assert.Fail("TODO (you)");
    }

    [Test]
    public void ValidateScopesFailsOnFirstResolveNotAtBuild()
    {
        // TODO (you): validateScopes: true, validateOnBuild: false. Assert BuildProvider does NOT throw,
        // then that resolving the singleton throws InvalidOperationException. When would that be in production?
        Assert.Fail("TODO (you)");
    }

    [Test]
    public void ValidateOnBuildFailsAtStartup()
    {
        // TODO (you): validateScopes: true, validateOnBuild: true. Assert BuildProvider itself throws,
        // and look at the exception type and message: they differ from the previous test.
        Assert.Fail("TODO (you)");
    }

    [Test]
    public void FixedServiceGetsANewInstancePerScope()
    {
        // TODO (you): register the fixed singleton (IServiceScopeFactory) with both validations on.
        // Assert the build succeeds, each call sees a different TenantContext, and each one is disposed.
        Assert.Fail("TODO (you)");
    }
}
