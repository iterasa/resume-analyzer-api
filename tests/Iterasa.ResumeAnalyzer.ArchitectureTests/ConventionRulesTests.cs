using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace Iterasa.ResumeAnalyzer.ArchitectureTests;

/// <summary>
/// Convenciones que el README promete y que, sin un test, solo duran hasta el siguiente
/// refactor con prisa.
/// </summary>
public sealed class ConventionRulesTests {

    private static readonly Assembly SharedAssembly = Assembly.Load("Iterasa.ResumeAnalyzer.Shared");
    private static readonly Assembly DomainAssembly = Assembly.Load("Iterasa.ResumeAnalyzer.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load("Iterasa.ResumeAnalyzer.Application");
    private static readonly Assembly ApiAssembly = Assembly.Load("Iterasa.ResumeAnalyzer.Api");

    /// <remarks>
    /// El instante lo recibe cada verbo del dominio y lo toma cada caso de uso de
    /// <see cref="TimeProvider" />: es lo que hace deterministas bloqueo, expiración y
    /// purga. Un <c>UtcNow</c> suelto rompe eso sin que ningún test funcional lo note.
    /// </remarks>
    [Theory]
    [InlineData("Iterasa.ResumeAnalyzer.Domain")]
    [InlineData("Iterasa.ResumeAnalyzer.Application")]
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
            .ResideInNamespaceStartingWith("Iterasa.ResumeAnalyzer.Application.Features")
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
            .ResideInNamespace("Iterasa.ResumeAnalyzer.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Iterasa.ResumeAnalyzer.Application.Abstractions",
                "Iterasa.ResumeAnalyzer.Infrastructure",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();

    }

    /// <summary>
    /// Las únicas acciones anónimas: sin ellas no se puede llegar a tener sesión (ADR 0017).
    /// </summary>
    private static readonly string[] AnonymousActions = [
        "AntiforgeryController.GetToken",
        "AuthController.Register",
        "AuthController.SignIn",
    ];

    /// <remarks>
    /// La API está cerrada por defecto (ADR 0016). Abrir una acción de un controller es una
    /// decisión de seguridad que no puede colarse con un atributo: si hace falta, se añade a
    /// <see cref="AnonymousActions" /> en el mismo PR, a la vista de quien revisa. Un controller
    /// entero nunca es anónimo.
    /// </remarks>
    [Fact]
    public void Only_The_Listed_Actions_Allow_Anonymous_Access() {

        var anonymous = ApiAssembly.GetTypes()
            .Where(type => type.Namespace == "Iterasa.ResumeAnalyzer.Api.Controllers")
            .SelectMany(type => type.GetMethods().Cast<System.Reflection.MemberInfo>().Prepend(type))
            .Where(member => member.IsDefined(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), inherit: true))
            .Select(member => member is Type type ? type.Name : $"{member.DeclaringType?.Name}.{member.Name}");

        anonymous.Should().BeEquivalentTo(AnonymousActions);

    }

    /// <remarks>
    /// Sin política, un endpoint no tiene límite: cada controller declara la suya (ADR 0019).
    /// </remarks>
    [Fact]
    public void Controllers_Should_Declare_A_Rate_Limit_Policy() {

        var unlimited = ApiAssembly.GetTypes()
            .Where(type => type.Namespace == "Iterasa.ResumeAnalyzer.Api.Controllers" && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type))
            .Where(type => !type.IsDefined(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), inherit: true))
            .Select(type => type.Name);

        unlimited.Should().BeEmpty();

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
                "Iterasa.ResumeAnalyzer.Domain",
                "Iterasa.ResumeAnalyzer.Application",
                "Iterasa.ResumeAnalyzer.Infrastructure",
                "Iterasa.ResumeAnalyzer.Api",
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