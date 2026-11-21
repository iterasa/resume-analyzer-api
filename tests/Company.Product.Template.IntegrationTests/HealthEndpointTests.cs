using FluentAssertions;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Company.Product.Template.IntegrationTests;

/// <summary>
/// Sondas de salud: la liveness no depende de la base de datos, la readiness sí.
/// </summary>
public sealed class HealthEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory> {

    /// <remarks>
    /// Estas dos sondas no consultan la base de datos: un PostgreSQL caído no debe
    /// provocar el reinicio del contenedor.
    /// </remarks>
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    public async Task Liveness_Probes_Should_Not_Depend_On_The_Database(string route) {

        var response = await factory.CreateClient().GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("status").GetString().Should().Be("Healthy");

    }

    /// <remarks>
    /// La suite sustituye PostgreSQL por SQLite en memoria, así que la readiness ve una
    /// base de datos alcanzable. El camino contrario —503 con PostgreSQL caído— se
    /// comprueba contra la aplicación real, no aquí.
    /// </remarks>
    [Fact]
    public async Task Readiness_Probe_Should_Be_Healthy_When_The_Database_Is_Reachable() {

        var response = await factory.CreateClient().GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("status").GetString().Should().Be("Healthy");

    }

    /// <remarks>
    /// Las sondas son anónimas: qué dependencia falló y por qué solo viaja al log.
    /// </remarks>
    [Fact]
    public async Task Probes_Should_Not_Disclose_Internal_Details() {

        var body = await (await factory.CreateClient().GetAsync("/health/ready")).Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        document.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(["status"]);
        body.Should().NotContainAny("database", "exception", "Npgsql", "127.0.0.1");

    }

}