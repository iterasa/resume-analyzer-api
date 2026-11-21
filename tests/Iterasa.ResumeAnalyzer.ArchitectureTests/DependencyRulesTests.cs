using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace Iterasa.ResumeAnalyzer.ArchitectureTests;

public sealed class DependencyRulesTests {

    private static readonly Assembly DomainAssembly = Assembly.Load("Iterasa.ResumeAnalyzer.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load("Iterasa.ResumeAnalyzer.Application");
    private static readonly Assembly ApiAssembly = Assembly.Load("Iterasa.ResumeAnalyzer.Api");

    [Fact]
    public void Domain_Should_Not_Depend_On_Api_Or_Infrastructure() {
        var result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Iterasa.ResumeAnalyzer.Api",
                "Iterasa.ResumeAnalyzer.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Api() {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Iterasa.ResumeAnalyzer.Api",
                "Iterasa.ResumeAnalyzer.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Api_Should_Not_Depend_On_Domain_Directly() {
        var result = Types
            .InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOn("Iterasa.ResumeAnalyzer.Domain")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Application() {
        var result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Iterasa.ResumeAnalyzer.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

}