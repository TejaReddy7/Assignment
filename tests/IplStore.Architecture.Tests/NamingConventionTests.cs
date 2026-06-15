using System.Reflection;
using IplStore.Shared;
using MediatR;
using NetArchTest.Rules;

namespace IplStore.Architecture.Tests;

/// <summary>
/// Enforces naming + structural conventions so the codebase stays predictable as it grows.
/// </summary>
public class NamingConventionTests
{
    private static readonly Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;

    [Fact]
    public void RequestHandlers_Should_HaveHandlerSuffix()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .Should()
            .HaveNameEndingWith("Handler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All MediatR handlers should end with 'Handler'. Offenders: {Describe(result)}");
    }

    [Fact]
    public void Validators_Should_HaveValidatorSuffix()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .Inherit(typeof(FluentValidation.AbstractValidator<>))
            .Should()
            .HaveNameEndingWith("Validator")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All validators should end with 'Validator'. Offenders: {Describe(result)}");
    }

    [Fact]
    public void Commands_And_Queries_Should_Be_Sealed()
    {
        // Records used as MediatR requests should be sealed (no inheritance intended).
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(IRequest<>))
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Commands/queries should be sealed. Offenders: {Describe(result)}");
    }

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null ? "none" : string.Join(", ", result.FailingTypeNames);
}
