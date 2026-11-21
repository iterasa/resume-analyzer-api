using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace Company.Product.Template.ArchitectureTests;

public sealed class DependencyRulesTests {

    private static readonly Assembly DomainAssembly = Assembly.Load("Company.Product.Template.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load("Company.Product.Template.Application");
    private static readonly Assembly ApiAssembly = Assembly.Load("Company.Product.Template.Api");

    [Fact]
    public void Domain_Should_Not_Depend_On_Api_Or_Infrastructure() {
        var result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Company.Product.Template.Api",
                "Company.Product.Template.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Api() {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Company.Product.Template.Api",
                "Company.Product.Template.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Api_Should_Not_Depend_On_Domain_Directly() {
        var result = Types
            .InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOn("Company.Product.Template.Domain")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Application() {
        var result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Company.Product.Template.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

}