using FluentAssertions;
using Iterasa.ResumeAnalyzer.Api.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

/// <summary>Inicio de sesión y registro limitados por IP: acotan la fuerza bruta y la enumeración.</summary>
public sealed class RateLimitTests(RateLimitTests.StrictFactory factory) : IClassFixture<RateLimitTests.StrictFactory> {

    public sealed class StrictFactory : CustomWebApplicationFactory {
        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.PostConfigure<RateLimitingOptions>(options => {
                options.AuthPermitLimit = 2;
                options.AuthWindowSeconds = 60;
            }));
        }
    }

    /// <remarks>
    /// Con sesión también cuenta por IP: si no, cada cuenta registrada estrenaría cuota.
    /// Cada clase de pruebas tiene su propia factory, así que esta IP empieza en cero aquí.
    /// </remarks>
    [Fact]
    public async Task A_Session_Does_Not_Buy_A_Fresh_Sign_In_Quota() {

        await using var strict = new StrictFactory();
        var browser = strict.CreateBrowser();

        var registered = await browser.PostAsJsonAsync("/api/v1/auth/register", new {
            companyName = "Acme",
            country = "Guatemala",
            name = "Ana",
            email = $"{Guid.NewGuid():N}@example.test",
            password = CustomWebApplicationFactory.Password,
            acceptTerms = true,
        });
        registered.StatusCode.Should().Be(HttpStatusCode.Created);

        (await browser.PostAsJsonAsync("/api/v1/auth/login", new { email = "x@example.test", password = "not the password", rememberMe = false })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await browser.PostAsJsonAsync("/api/v1/auth/login", new { email = "x@example.test", password = "not the password", rememberMe = false })).StatusCode
            .Should().Be(HttpStatusCode.TooManyRequests, "the session cookie does not move the request to a per-user quota");

    }

    [Theory]
    [InlineData("2001:db8:1:2:aaaa::1", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2:bbbb:cccc:dddd:eeee", "2001:db8:1:2::/64")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("203.0.113.7", "203.0.113.7")]
    public void IPv6_Clients_Share_The_Quota_Of_Their_64(string address, string partition) {
        RateLimitingExtensions.ClientNetwork(System.Net.IPAddress.Parse(address)).Should().Be(partition);
    }

    [Fact]
    public async Task The_Third_Sign_In_Attempt_In_The_Window_Gets_429_With_Retry_After() {

        var browser = factory.CreateBrowser();
        var attempt = () => browser.PostAsJsonAsync("/api/v1/auth/login", new { email = "ana@example.test", password = "not the password", rememberMe = false });

        (await attempt()).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await attempt()).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var limited = await attempt();

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.Contains("Retry-After").Should().BeTrue();
        using var problem = await ProblemDetailsTests.ReadProblemAsync(limited);
        problem.RootElement.GetProperty("type").GetString().Should().Be("urn:problem-type:too_many_requests");

    }

}