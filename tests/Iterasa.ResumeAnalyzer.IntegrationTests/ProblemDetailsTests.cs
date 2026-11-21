using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

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

        var admin = await factory.RegisterAsync();

        var fromContract = await admin.Browser.PostAsJsonAsync("/api/v1/vacancies", new { modality = "Remote" });
        var fromDomain = await admin.Browser.PostAsJsonAsync("/api/v1/vacancies", VacancyEndpointTests.NewVacancy(admin.UserId, title: new string('a', 121)));

        fromContract.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fromDomain.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var contract = await ReadProblemAsync(fromContract);
        using var domain = await ReadProblemAsync(fromDomain);

        contract.RootElement.GetProperty("type").GetString().Should().Be(TypePrefix + "validation_error");
        domain.RootElement.GetProperty("type").GetString().Should().Be(TypePrefix + "validation_error");
        domain.RootElement.GetProperty("title").GetString().Should().Be(contract.RootElement.GetProperty("title").GetString());

        contract.RootElement.GetProperty("errors").TryGetProperty("Title", out _).Should().BeTrue("the contract names the field");
        domain.RootElement.GetProperty("errors").GetProperty("").EnumerateArray().Single().GetString().Should().Be("Title cannot exceed 120 characters.");

    }

    [Fact]
    public async Task Use_Case_Errors_Should_Publish_Their_Code_As_The_Type() {

        var admin = await factory.RegisterAsync();
        var response = await admin.Browser.GetAsync($"/api/v1/vacancies/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var problem = await ReadProblemAsync(response);
        problem.RootElement.GetProperty("type").GetString().Should().Be(TypePrefix + "not_found");
        problem.RootElement.GetProperty("title").GetString().Should().Be("Not Found");
        problem.RootElement.GetProperty("detail").GetString().Should().Be("Vacancy not found.");
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(404);

    }

    [Fact]
    public async Task Conflicts_Should_Publish_Their_Code_As_The_Type() {

        var admin = await factory.RegisterAsync();
        var browser = factory.CreateBrowser();

        var response = await browser.PostAsJsonAsync("/api/v1/auth/register", new {
            companyName = "Otra",
            country = "Honduras",
            name = "Bea",
            email = admin.Email.ToUpperInvariant(),
            password = CustomWebApplicationFactory.Password,
            acceptTerms = true,
        });

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
    [InlineData("PATCH", "/api/v1/vacancies", HttpStatusCode.MethodNotAllowed, "method_not_allowed")]
    public async Task Framework_Errors_Should_Be_Normalized_To_The_Catalogue(string method, string path, HttpStatusCode expectedStatus, string expectedCode) {

        var admin = await factory.RegisterAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await admin.Browser.SendAsync(request);

        response.StatusCode.Should().Be(expectedStatus);

        using var problem = await ReadProblemAsync(response);
        problem.RootElement.GetProperty("type").GetString().Should().Be(TypePrefix + expectedCode);
        problem.RootElement.GetProperty("status").GetInt32().Should().Be((int)expectedStatus);
        problem.RootElement.TryGetProperty("correlationId", out _).Should().BeTrue();

    }

    internal static async Task<JsonDocument> ReadProblemAsync(HttpResponseMessage response) {
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

}