using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Company.Product.Template.IntegrationTests;

/// <summary>
/// Una única forma de error para el llamante: todo fallo, lo produzca quien lo produzca,
/// es un Problem Details con un <c>type</c> del catálogo de la API.
/// </summary>
public sealed class ProblemDetailsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory> {

    private const string TypePrefix = "urn:problem-type:";

    /// <remarks>
    /// Un campo obligatorio ausente (validación del contrato HTTP) y una invariante del
    /// dominio incumplida (la lanza el agregado y la traduce el manejador global) deben
    /// verse igual: mismo tipo, mismo título, mismo diccionario <c>errors</c>.
    /// </remarks>
    [Fact]
    public async Task Contract_Validation_And_Domain_Invariants_Should_Share_One_Shape() {

        var client = factory.CreateClient();

        var fromContract = await client.PostAsJsonAsync("/api/v1/users", new { username = "no-email" });
        var fromDomain = await client.PostAsJsonAsync("/api/v1/users", new { username = new string('a', 101), email = "long@example.test" });

        fromContract.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fromDomain.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var contract = await ReadProblemAsync(fromContract);
        using var domain = await ReadProblemAsync(fromDomain);

        contract.RootElement.GetProperty("type").GetString().Should().Be(TypePrefix + "validation_error");
        domain.RootElement.GetProperty("type").GetString().Should().Be(TypePrefix + "validation_error");
        domain.RootElement.GetProperty("title").GetString().Should().Be(contract.RootElement.GetProperty("title").GetString());

        contract.RootElement.GetProperty("errors").TryGetProperty("Email", out _).Should().BeTrue("the contract names the field");
        domain.RootElement.GetProperty("errors").GetProperty("").EnumerateArray().Single().GetString().Should().Be("Username cannot exceed 100 characters.");

    }

    [Fact]
    public async Task Use_Case_Errors_Should_Publish_Their_Code_As_The_Type() {

        var response = await factory.CreateClient().GetAsync($"/api/v1/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var problem = await ReadProblemAsync(response);
        problem.RootElement.GetProperty("type").GetString().Should().Be(TypePrefix + "not_found");
        problem.RootElement.GetProperty("title").GetString().Should().Be("Not Found");
        problem.RootElement.GetProperty("detail").GetString().Should().Be("User not found.");
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(404);

    }

    [Fact]
    public async Task Conflicts_Should_Publish_Their_Code_As_The_Type() {

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/users", new { username = CustomWebApplicationFactory.SeededUsername, email = "conflict@example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var problem = await ReadProblemAsync(response);
        problem.RootElement.GetProperty("type").GetString().Should().Be(TypePrefix + "conflict");
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(409);

    }

    /// <remarks>
    /// Los errores que produce el propio framework —ruta inexistente, método no admitido—
    /// no pasan por ningún controller, y aun así salen con el tipo del catálogo.
    /// </remarks>
    [Theory]
    [InlineData("GET", "/api/v1/route-that-does-not-exist", HttpStatusCode.NotFound, "not_found")]
    [InlineData("PUT", "/api/v1/users", HttpStatusCode.MethodNotAllowed, "method_not_allowed")]
    public async Task Framework_Errors_Should_Be_Normalized_To_The_Catalogue(string method, string path, HttpStatusCode expectedStatus, string expectedCode) {

        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(expectedStatus);

        using var problem = await ReadProblemAsync(response);
        problem.RootElement.GetProperty("type").GetString().Should().Be(TypePrefix + expectedCode);
        problem.RootElement.GetProperty("status").GetInt32().Should().Be((int)expectedStatus);
        problem.RootElement.TryGetProperty("correlationId", out _).Should().BeTrue();

    }

    private static async Task<JsonDocument> ReadProblemAsync(HttpResponseMessage response) {
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

}