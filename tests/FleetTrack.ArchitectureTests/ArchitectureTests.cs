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
    private static readonly Architecture Architecture = new ArchLoader().LoadAssemblies(
            typeof(ApplicationAssemblyMarker).Assembly,
            typeof(DomainAssemblyMarker).Assembly,
            typeof(InfrastructureAssemblyMarker).Assembly,
            typeof(Program).Assembly
        ).Build();

    private static readonly IObjectProvider<IType> DomainLayer =
      Types().That().ResideInAssembly(typeof(DomainAssemblyMarker).Assembly).As("Domain");

    private static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInAssembly(typeof(ApplicationAssemblyMarker).Assembly).As("Application");

    private static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInAssembly(typeof(InfrastructureAssemblyMarker).Assembly).As("Infrastructure");

    private static readonly IObjectProvider<IType> ApiLayer =
        Types().That().ResideInAssembly(typeof(Program).Assembly).As("API");


    [Test]
    public void Domain_ShouldNotDependOn_Application()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
        .Should()
        .NotDependOnAny(ApplicationLayer)
        .Because("Domain is the core and must not know about outer layers (ADR-003)");

        rule.Check(Architecture);
    }

    [Test]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
        .Should()
        .NotDependOnAny(InfrastructureLayer)
        .Because("Domain is the core and must not know about outer layers (ADR-003)");

        rule.Check(Architecture);
    }

    [Test]
    public void Domain_ShouldNotDependOn_API()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
        .Should()
        .NotDependOnAny(ApiLayer)
        .Because("Domain is the core and must not know about outer layers (ADR-003)");

        rule.Check(Architecture);
    }

    [Test]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        IArchRule rule = Types().That().Are(ApplicationLayer)
        .Should()
        .NotDependOnAny(InfrastructureLayer)
        .Because("Application layer must not know about infrastructure details (ADR-003)");

        rule.Check(Architecture);
    }

    [Test]
    public void Application_ShouldNotDependOn_API()
    {
        IArchRule rule = Types().That().Are(ApplicationLayer)
        .Should()
        .NotDependOnAny(ApiLayer)
        .Because("Application layer must not know about API details (ADR-003)");

        rule.Check(Architecture);
    }

    [Test]
    public void Domain_ShouldNotDependOn_Framework()
    {
        IArchRule rule = Types().That()
        .ResideInAssembly(typeof(DomainAssemblyMarker).Assembly)
        .Should()
        .NotDependOnAny(Types(includeReferenced: true).That()
        .ResideInNamespaceMatching("Microsoft"))
        .Because("Domain is the core and must not know about outer layers (ADR-003)");

        rule.Check(Architecture);
    }

}
