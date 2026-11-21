using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace Company.Product.Template.ArchitectureTests;

/// <summary>
/// Convenciones que el README promete y que, sin un test, solo duran hasta el siguiente
/// refactor con prisa.
/// </summary>
public sealed class ConventionRulesTests {

    private static readonly Assembly SharedAssembly = Assembly.Load("Company.Product.Template.Shared");
    private static readonly Assembly DomainAssembly = Assembly.Load("Company.Product.Template.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load("Company.Product.Template.Application");
    private static readonly Assembly ApiAssembly = Assembly.Load("Company.Product.Template.Api");

    /// <remarks>
    /// El instante lo recibe cada verbo del dominio y lo toma cada caso de uso de
    /// <see cref="TimeProvider" />: es lo que hace deterministas bloqueo, expiración y
    /// purga. Un <c>UtcNow</c> suelto rompe eso sin que ningún test funcional lo note.
    /// </remarks>
    [Theory]
    [InlineData("Company.Product.Template.Domain")]
    [InlineData("Company.Product.Template.Application")]
    public void Domain_And_Application_Should_Not_Read_The_System_Clock(string assemblyName) {

        var result = Types
            .InAssembly(Assembly.Load(assemblyName))
            .Should()
            .MeetCustomRule(new DoesNotReadSystemClock())
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty("only TimeProvider may provide the current instant");

    }

    [Fact]
    public void Handlers_Should_Be_Internal_Sealed_And_Implement_Their_Own_Interface() {

        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespaceStartingWith("Company.Product.Template.Application.Features")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .And()
            .NotBePublic()
            .And()
            .MeetCustomRule(new ImplementsInterfaceNamedAfterItself())
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();

    }

    /// <remarks>
    /// Un controller habla con casos de uso, nunca con repositorios, consultas ni la
    /// unidad de trabajo: si pudiera, la lógica acabaría en la capa HTTP.
    /// </remarks>
    [Fact]
    public void Controllers_Should_Depend_Only_On_Use_Cases() {

        var result = Types
            .InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("Company.Product.Template.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Company.Product.Template.Application.Abstractions",
                "Company.Product.Template.Infrastructure",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();

    }

    [Fact]
    public void Domain_Should_Depend_On_Nothing_But_The_Base_Library_And_Shared() {

        var result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft", "Npgsql", "Newtonsoft", "System.Data")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();

    }

    [Fact]
    public void Application_Should_Not_Know_The_Database_Or_The_Web_Framework() {

        var result = Types
            .InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "System.Data")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();

    }

    [Fact]
    public void Shared_Should_Be_A_Leaf() {

        var result = Types
            .InAssembly(SharedAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Company.Product.Template.Domain",
                "Company.Product.Template.Application",
                "Company.Product.Template.Infrastructure",
                "Company.Product.Template.Api",
                "Microsoft")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();

    }

    /// <summary>
    /// Recorre el IL del tipo y de todos sus anidados: ahí es donde el compilador deja
    /// las lambdas y las máquinas de estado de los métodos <c>async</c>, que es donde
    /// se esconde un <c>UtcNow</c> que una búsqueda por tipo no ve.
    /// </summary>
    private sealed class DoesNotReadSystemClock : ICustomRule {

        private static readonly HashSet<string> ClockTypes = new(StringComparer.Ordinal) { "System.DateTime", "System.DateTimeOffset" };
        private static readonly HashSet<string> ClockMembers = new(StringComparer.Ordinal) { "get_UtcNow", "get_Now", "get_Today" };

        public bool MeetsRule(TypeDefinition type) =>
            !ReadsClock(type);

        private static bool ReadsClock(TypeDefinition type) =>
            type.Methods.Where(method => method.HasBody).Any(method => method.Body.Instructions.Any(IsClockRead))
            || type.NestedTypes.Any(ReadsClock);

        private static bool IsClockRead(Instruction instruction) =>
            instruction.OpCode.Code is Code.Call or Code.Callvirt
            && instruction.Operand is MethodReference target
            && ClockTypes.Contains(target.DeclaringType.FullName)
            && ClockMembers.Contains(target.Name);

    }

    private sealed class ImplementsInterfaceNamedAfterItself : ICustomRule {
        public bool MeetsRule(TypeDefinition type) =>
            type.Interfaces.Any(implemented => implemented.InterfaceType.Name == $"I{type.Name}");
    }

}