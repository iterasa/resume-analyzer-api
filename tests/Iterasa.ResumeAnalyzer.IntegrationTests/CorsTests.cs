using FluentAssertions;
using Iterasa.ResumeAnalyzer.Api.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

/// <summary>
/// Política CORS: lista explícita de orígenes, sin credenciales por defecto, y cabeceras
/// también en las respuestas de error.
/// </summary>
/// <remarks>
/// La política se activa sobrescribiendo las opciones en el contenedor, no con variables
/// de entorno: estas son globales al proceso y contaminarían a las demás factories que
/// arrancan en paralelo.
/// </remarks>
public sealed class CorsTests(CorsTests.CorsEnabledFactory factory) : IClassFixture<CorsTests.CorsEnabledFactory> {

    public const string AppOrigin = "https://app.example.test";
    public const string WildcardOrigin = "https://*.tenant.example.test";
    public const string LocalOrigin = "http://localhost:5173";

    public sealed class CorsEnabledFactory : CustomWebApplicationFactory {
        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.PostConfigure<CorsPolicyOptions>(options => {
                options.Enabled = true;
                options.AllowedOrigins = [AppOrigin, WildcardOrigin, LocalOrigin];
                options.PreflightMaxAgeSeconds = 600;
            }));
        }
    }

    [Fact]
    public async Task Preflight_From_An_Allowed_Origin_Should_Grant_Only_What_The_Api_Uses() {

        using var preflight = new HttpRequestMessage(HttpMethod.Options, $"/api/v1/vacancies/{Guid.NewGuid()}");
        preflight.Headers.Add("Origin", AppOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "PUT");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type, x-xsrf-token, idempotency-key");

        var response = await factory.CreateClient().SendAsync(preflight);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(AppOrigin);
        response.Headers.GetValues("Access-Control-Allow-Methods").Should().ContainSingle().Which.Should().Contain("PUT").And.NotContain("PATCH");
        string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant().Should().Contain("x-xsrf-token").And.Contain("content-type").And.Contain("idempotency-key");
        response.Headers.GetValues("Access-Control-Max-Age").Should().ContainSingle().Which.Should().Be("600");
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse("credentials are off unless configured");

    }

    [Theory]
    [InlineData("https://evil.example.test")]
    [InlineData("https://app.example.test.evil.test")]
    [InlineData("https://app.example.test:8443")]
    [InlineData("http://app.example.test")]
    [InlineData("https://tenant.example.test", "the wildcard covers subdomains, not the apex")]
    [InlineData("null")]
    public async Task Preflight_From_Any_Other_Origin_Should_Grant_Nothing(string origin, string? because = null) {

        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/vacancies");
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await factory.CreateClient().SendAsync(preflight);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse(because ?? "the origin is not in the allowlist");

    }

    [Theory]
    [InlineData("https://acme.tenant.example.test")]
    [InlineData("https://eu.acme.tenant.example.test")]
    [InlineData("http://localhost:5173")]
    public async Task Wildcard_And_Local_Origins_Should_Be_Allowed(string origin) {

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/vacancies");
        request.Headers.Add("Origin", origin);

        var response = await factory.CreateClient().SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(origin);

    }

    /// <remarks>
    /// Sin la cabecera en una respuesta de error, el navegador la oculta al script y el
    /// cliente ve un error de red genérico en lugar del Problem Details que explica qué pasó.
    /// </remarks>
    [Fact]
    public async Task Error_Responses_Should_Carry_The_Cors_Headers_And_Expose_The_Correlation_Id() {

        var admin = await factory.RegisterAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/vacancies/{Guid.NewGuid()}");
        request.Headers.Add("Origin", AppOrigin);

        var response = await admin.Browser.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(AppOrigin);
        response.Headers.GetValues("Access-Control-Expose-Headers").Should().ContainSingle().Which.Should().Contain("X-Correlation-Id").And.Contain("Retry-After");
        response.Headers.Vary.Should().Contain("Origin");

    }

    [Fact]
    public async Task Cors_Should_Be_Off_Unless_Configured() {

        using var disabled = new CustomWebApplicationFactory();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", AppOrigin);

        var response = await disabled.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();

    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://app.example.test/api")]
    [InlineData("app.example.test")]
    [InlineData("")]
    public void The_Host_Should_Refuse_To_Start_With_An_Invalid_Origin(string origin) {

        using var broken = new BrokenCorsFactory(origin);

        var act = () => broken.CreateClient();

        act.Should().Throw<Exception>().Which.ToString().Should().Contain("CORS_ALLOWED_ORIGINS");

    }

    private sealed class BrokenCorsFactory(string origin) : CustomWebApplicationFactory {
        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.PostConfigure<CorsPolicyOptions>(options => {
                options.Enabled = true;
                options.AllowedOrigins = origin.Length == 0 ? [] : [origin];
            }));
        }
    }

}