using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

/// <summary>
/// La API está cerrada por defecto (ADR 0016): sin sesión no se lee ni se cambia nada.
/// </summary>
public sealed class AuthorizationTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory> {

    [Fact]
    public async Task Without_A_Session_Nothing_Can_Be_Read_Or_Changed() {

        var admin = await factory.RegisterAsync();
        var created = await admin.Browser.PostAsJsonAsync("/api/v1/vacancies", VacancyEndpointTests.NewVacancy(admin.UserId));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var client = factory.CreateAnonymousClient();

        var responses = new[] {
            await client.GetAsync("/api/v1/me"),
            await client.GetAsync("/api/v1/vacancies"),
            await client.GetAsync($"/api/v1/vacancies/{id}"),
            await client.PostAsJsonAsync("/api/v1/vacancies", VacancyEndpointTests.NewVacancy(admin.UserId)),
            await client.PostAsync($"/api/v1/vacancies/{id}/pause", content: null),
            await client.PostAsync("/api/v1/auth/logout", content: null),
        };

        foreach (var response in responses) {
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
            var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            problem.GetProperty("type").GetString().Should().Be("urn:problem-type:unauthorized");
        }

        var detail = await admin.Browser.GetFromJsonAsync<JsonElement>($"/api/v1/vacancies/{id}");
        detail.GetProperty("status").GetString().Should().Be("Open", "nothing changed");

    }

    [Fact]
    public async Task A_Forged_Or_Unknown_Session_Cookie_Is_Rejected() {

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Add("Cookie", $"__Host-criba_session={new string('A', 43)}");

        var response = await factory.CreateDefaultClient(new Uri("https://localhost")).SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_Probes_Stay_Anonymous(string path) {
        var response = await factory.CreateAnonymousClient().GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <remarks>
    /// Sin sesión, una ruta de la API que no existe también responde 401: quien no inició
    /// sesión no puede averiguar qué rutas existen. Con sesión es un 404 (ProblemDetailsTests).
    /// </remarks>
    [Fact]
    public async Task Without_A_Session_The_Api_Does_Not_Reveal_Which_Routes_Exist() {
        var response = await factory.CreateAnonymousClient().GetAsync("/api/v1/nothing-here");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

}