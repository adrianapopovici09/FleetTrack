using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.NUnit;

using FleetTrack.Application;
using FleetTrack.Domain;
using FleetTrack.Infrastructure;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace FleetTrack.ArchitectureTests;

internal sealed class ArchitectureTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(DomainAssemblyMarker).Assembly,
            typeof(ApplicationAssemblyMarker).Assembly,
            typeof(InfrastructureAssemblyMarker).Assembly,
            typeof(Program).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInAssembly(typeof(DomainAssemblyMarker).Assembly).As("Domain");

    private static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInAssembly(typeof(ApplicationAssemblyMarker).Assembly).As("Application");

    private static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInAssembly(typeof(InfrastructureAssemblyMarker).Assembly).As("Infrastructure");

    private static readonly IObjectProvider<IType> ApiLayer =
        Types().That().ResideInAssembly(typeof(Program).Assembly).As("API");

    // includeReferenced: framework types live outside the loaded assemblies, so plain Types() would never match them.
    // Anchored so only "Microsoft" and "Microsoft.*" match, not e.g. "FleetTrack.Integrations.MicrosoftGraph".
    private static readonly IObjectProvider<IType> FrameworkTypes =
        Types(includeReferenced: true).That().ResideInNamespaceMatching(@"^Microsoft(\..*)?$").As("Microsoft.* frameworks");

    [Test]
    public void Domain_ShouldNotDependOn_Application()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(ApplicationLayer)
            .Because("Domain is the core and must not know about outer layers (ADR-003)");

        rule.Check(Architecture);
    }

    [Test]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .Because("Domain is the core and must not know about outer layers (ADR-003)");

        rule.Check(Architecture);
    }

    [Test]
    public void Domain_ShouldNotDependOn_API()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(ApiLayer)
            .Because("Domain is the core and must not know about outer layers (ADR-003)");

        rule.Check(Architecture);
    }

    [Test]
    public void Domain_ShouldNotDependOn_Framework()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(FrameworkTypes)
            .Because("Domain must be plain C#: no framework, testable in isolation (ADR-003)");

        rule.Check(Architecture);
    }

    [Test]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        IArchRule rule = Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .Because("Application layer must not know about infrastructure details (ADR-003)");

        rule.Check(Architecture);
    }

    [Test]
    public void Application_ShouldNotDependOn_API()
    {
        IArchRule rule = Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAny(ApiLayer)
            .Because("Application layer must not know about API details (ADR-003)");

        rule.Check(Architecture);
    }
}