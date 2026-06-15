using System.Reflection;
using NetArchTest.Rules;

namespace IplStore.Architecture.Tests;

/// <summary>
/// Automated guardrails that enforce the Clean Architecture dependency rule.
/// These fail the build if someone accidentally points a dependency the wrong way.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Domain.Entities.Product).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.DependencyInjection).Assembly;

    private const string DomainNamespace = "IplStore.Domain";
    private const string ApplicationNamespace = "IplStore.Application";
    private const string InfrastructureNamespace = "IplStore.Infrastructure";
    private const string ApiNamespace = "IplStore.Api";

    [Fact]
    public void Domain_Should_Not_DependOn_OtherLayers()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain must not depend on outer layers. Offenders: {Describe(result)}");
    }

    [Fact]
    public void Domain_Should_Not_DependOn_EntityFrameworkCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain must stay persistence-ignorant. Offenders: {Describe(result)}");
    }

    [Fact]
    public void Application_Should_Not_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application must not depend on Infrastructure (Dependency Inversion). Offenders: {Describe(result)}");
    }

    [Fact]
    public void Application_Should_Not_DependOn_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue($"Application must not depend on the API layer. Offenders: {Describe(result)}");
    }

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null ? "none" : string.Join(", ", result.FailingTypeNames);
}
