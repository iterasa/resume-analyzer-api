using FluentAssertions;
using System.Net;
using Xunit;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

/// <summary>Cabeceras de endurecimiento en toda respuesta, salga por donde salga.</summary>
public sealed class SecurityHeadersTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory> {

    [Theory]
    [InlineData("/health", HttpStatusCode.OK)]
    [InlineData("/api/v1/users", HttpStatusCode.OK)]
    [InlineData("/api/v1/route-that-does-not-exist", HttpStatusCode.NotFound)]
    public async Task Every_Response_Should_Carry_The_Security_Headers(string path, HttpStatusCode expectedStatus) {

        var response = await factory.CreateClient().GetAsync(path);

        response.StatusCode.Should().Be(expectedStatus);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle().Which.Should().Be("DENY");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle().Which.Should().Be("frame-ancestors 'none'");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.Contains("Server").Should().BeFalse();

    }

}