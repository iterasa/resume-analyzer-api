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
    public async Task Without_A_Session_Users_Cannot_Be_Listed_Read_Created_Or_Disabled() {

        var client = factory.CreateAnonymousClient();
        var id = factory.SeededUserId;

        var responses = new[] {
            await client.GetAsync("/api/v1/users"),
            await client.GetAsync($"/api/v1/users/{id}"),
            await client.PostAsJsonAsync("/api/v1/users", new { username = "intruder", email = "intruder@example.test" }),
            await client.DeleteAsync($"/api/v1/users/{id}"),
            await client.PostAsync($"/api/v1/users/{id}/activation", content: null),
        };

        foreach (var response in responses) {
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
            var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            problem.GetProperty("type").GetString().Should().Be("urn:problem-type:unauthorized");
            (await response.Content.ReadAsStringAsync()).Should().NotContain("user@example.test");
        }

        // Nada cambió: el usuario sembrado sigue activo y el intruso no existe.
        var seeded = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/users/{id}");
        seeded.GetProperty("isEnabled").GetBoolean().Should().BeTrue();

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