using FluentAssertions;
using Iterasa.ResumeAnalyzer.Domain.Tenants;
using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

/// <summary>
/// Sesión del lado del servidor (ADR 0017): registro, inicio y cierre, cambio de empresa,
/// bloqueo por intentos y antiforgery.
/// </summary>
public sealed class AuthEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory> {

    private const string SessionCookie = "__Host-criba_session";

    [Fact]
    public async Task Registering_Creates_The_Company_And_Opens_An_Admin_Session() {

        var browser = factory.CreateBrowser();
        var email = $"ana-{Guid.NewGuid():N}@example.test";

        var response = await browser.PostAsJsonAsync("/api/v1/auth/register", new {
            companyName = "Altamira",
            country = "Guatemala",
            name = "Ana Morales",
            email,
            password = CustomWebApplicationFactory.Password,
            acceptTerms = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var cookie = SetCookie(response, SessionCookie);
        cookie.Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=lax").And.Contain("path=/");
        cookie.Should().NotContain("expires", "without «remember me» it is a browser-session cookie");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(CookieValue(response, SessionCookie), "the session token never travels in the body");

        var session = JsonDocument.Parse(body).RootElement;
        session.GetProperty("user").GetProperty("email").GetString().Should().Be(email);
        session.GetProperty("user").GetProperty("name").GetString().Should().Be("Ana Morales");
        session.GetProperty("user").GetProperty("emailVerified").GetBoolean().Should().BeFalse();
        session.GetProperty("tenant").GetProperty("name").GetString().Should().Be("Altamira");
        session.GetProperty("tenant").GetProperty("plan").GetProperty("status").GetString().Should().Be("Trial");
        session.GetProperty("role").GetString().Should().Be("Admin");
        session.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should().Contain(["vacancies.manage", "team.manage", "billing.manage"]);
        session.GetProperty("memberships").GetArrayLength().Should().Be(1);
        session.GetProperty("anonymousReview").GetBoolean().Should().BeTrue("new members start with the company default, which is anonymous");

        var me = await browser.GetFromJsonAsync<JsonElement>("/api/v1/me");
        me.GetProperty("user").GetProperty("id").GetString().Should().Be(session.GetProperty("user").GetProperty("id").GetString());

    }

    [Fact]
    public async Task The_Company_Gets_Its_Own_Encryption_Keys_And_The_Name_Is_Stored_Encrypted() {

        var admin = await factory.RegisterAsync();

        await factory.InScopeAsync(async services => {
            var context = services.GetRequiredService<AppDbContext>();
            (await context.EncryptionKeys.CountAsync(key => key.ScopeId == admin.TenantId)).Should().Be(2);
            var stored = await context.Database.SqlQueryRaw<string>("SELECT name AS \"Value\" FROM \"user\"").ToListAsync();
            stored.Should().NotBeEmpty().And.AllSatisfy(name => name.Should().StartWith("enc1.").And.NotContain("Ana"));
        });

    }

    [Theory]
    [InlineData(true, "password", "short")]
    [InlineData(false, "acceptTerms", null)]
    public async Task Registering_Validates_Terms_And_Password(bool acceptTerms, string field, string? password) {

        var response = await factory.CreateBrowser().PostAsJsonAsync("/api/v1/auth/register", new {
            companyName = "Acme",
            country = "Guatemala",
            name = "Ana",
            email = $"{Guid.NewGuid():N}@example.test",
            password = password ?? CustomWebApplicationFactory.Password,
            acceptTerms,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var problem = await ProblemDetailsTests.ReadProblemAsync(response);
        problem.RootElement.GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue();

    }

    [Fact]
    public async Task Logging_Out_Revokes_The_Session_On_The_Server() {

        var admin = await factory.RegisterAsync();
        var cookie = await CurrentCookieAsync(admin.Browser);

        (await admin.Browser.PostAsync("/api/v1/auth/logout", content: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.Browser.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Quien hubiera copiado la cookie tampoco entra: la sesión no existe en el servidor.
        using var replay = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        replay.Headers.Add("Cookie", $"{SessionCookie}={cookie}");
        (await factory.CreateDefaultClient(new Uri("https://localhost")).SendAsync(replay)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    }

    [Fact]
    public async Task Signing_In_Opens_A_New_Session_And_Remember_Me_Makes_The_Cookie_Persistent() {

        var admin = await factory.RegisterAsync();
        var browser = factory.CreateBrowser();

        var response = await browser.PostAsJsonAsync("/api/v1/auth/login", new { email = admin.Email, password = CustomWebApplicationFactory.Password, rememberMe = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        SetCookie(response, SessionCookie).Should().Contain("expires");
        CookieValue(response, SessionCookie).Should().NotBe(await CurrentCookieAsync(admin.Browser));

        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        session.GetProperty("tenant").GetProperty("id").GetString().Should().Be(admin.TenantId.ToString());
        (await browser.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.OK);

    }

    [Fact]
    public async Task Wrong_Password_And_Unknown_Email_Look_The_Same() {

        var admin = await factory.RegisterAsync();
        var browser = factory.CreateBrowser();

        var wrong = await browser.PostAsJsonAsync("/api/v1/auth/login", new { email = admin.Email, password = "not the password", rememberMe = false });
        var unknown = await browser.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@example.test", password = "not the password", rememberMe = false });

        foreach (var response in new[] { wrong, unknown }) {
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            using var problem = await ProblemDetailsTests.ReadProblemAsync(response);
            problem.RootElement.GetProperty("type").GetString().Should().Be("urn:problem-type:invalid_credentials");
            problem.RootElement.GetProperty("detail").GetString().Should().Be("The email or password is incorrect.");
        }

    }

    [Fact]
    public async Task Five_Failed_Attempts_Lock_The_Account_Even_For_The_Right_Password() {

        var admin = await factory.RegisterAsync();
        var browser = factory.CreateBrowser();

        for (var attempt = 0; attempt < 5; attempt++) {
            await browser.PostAsJsonAsync("/api/v1/auth/login", new { email = admin.Email, password = "not the password", rememberMe = false });
        }

        var locked = await browser.PostAsJsonAsync("/api/v1/auth/login", new { email = admin.Email, password = CustomWebApplicationFactory.Password, rememberMe = false });

        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    }

    /// <remarks>
    /// Una ráfaga de intentos simultáneos cuenta entera: si se perdieran las sumas, se podrían
    /// probar contraseñas de cinco en cinco por ráfaga sin llegar nunca al bloqueo.
    /// </remarks>
    [Fact]
    public async Task Simultaneous_Failed_Attempts_All_Count_Toward_The_Lockout() {

        var admin = await factory.RegisterAsync();
        var browsers = Enumerable.Range(0, 8).Select(_ => factory.CreateBrowser()).ToList();

        var attempts = await Task.WhenAll(browsers.Select(browser =>
            browser.PostAsJsonAsync("/api/v1/auth/login", new { email = admin.Email, password = "not the password", rememberMe = false })));

        attempts.Should().AllSatisfy(response => response.StatusCode.Should().Be(HttpStatusCode.Unauthorized));

        var withTheRightPassword = await factory.CreateBrowser().PostAsJsonAsync("/api/v1/auth/login", new { email = admin.Email, password = CustomWebApplicationFactory.Password, rememberMe = false });
        withTheRightPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the burst locked the account");

    }

    [Fact]
    public async Task Signing_In_Again_Revokes_The_Session_The_Browser_Had() {

        var admin = await factory.RegisterAsync();
        var before = await CurrentCookieAsync(admin.Browser);

        (await admin.Browser.PostAsJsonAsync("/api/v1/auth/login", new { email = admin.Email, password = CustomWebApplicationFactory.Password, rememberMe = false }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using var stale = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        stale.Headers.Add("Cookie", $"{SessionCookie}={before}");
        (await factory.CreateDefaultClient(new Uri("https://localhost")).SendAsync(stale)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await admin.Browser.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.OK);

    }

    [Fact]
    public async Task Mutations_Without_The_Antiforgery_Token_Are_Rejected() {

        var browser = factory.CreateBrowser(antiforgery: false);

        var response = await browser.PostAsJsonAsync("/api/v1/auth/login", new { email = "ana@example.test", password = CustomWebApplicationFactory.Password, rememberMe = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var problem = await ProblemDetailsTests.ReadProblemAsync(response);
        problem.RootElement.GetProperty("type").GetString().Should().Be("urn:problem-type:antiforgery_invalid");

    }

    [Fact]
    public async Task The_Antiforgery_Token_Is_Readable_By_The_Web_And_Its_Pair_Is_Not() {

        var response = await factory.CreateBrowser().GetAsync("/api/v1/antiforgery/token");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        SetCookie(response, "XSRF-TOKEN").Should().NotContain("httponly").And.Contain("secure").And.Contain("samesite=strict");
        SetCookie(response, "__Host-criba_af").Should().Contain("httponly").And.Contain("secure");

    }

    [Fact]
    public async Task Switching_Company_Replaces_The_Session_And_Only_Works_For_Own_Companies() {

        var ana = await factory.RegisterAsync(companyName: "Primera");
        var other = await factory.RegisterAsync(companyName: "Segunda");

        (await ana.Browser.PostAsJsonAsync("/api/v1/auth/switch-tenant", new { tenantId = other.TenantId })).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "Ana is not a member of the second company");

        // Ana pasa a ser reclutadora de la segunda (la invitación llega con Equipo).
        await factory.InScopeAsync(async services => {
            using var _ = TenantCryptoScope.Enter(other.TenantId);
            var context = services.GetRequiredService<AppDbContext>();
            context.Memberships.Add(Membership.Create(Guid.NewGuid(), other.TenantId, ana.UserId, MemberRole.Recruiter, anonymousReview: false, other.UserId, DateTimeOffset.UtcNow));
            await context.SaveChangesAsync();
        });

        var before = await CurrentCookieAsync(ana.Browser);
        var response = await ana.Browser.PostAsJsonAsync("/api/v1/auth/switch-tenant", new { tenantId = other.TenantId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        session.GetProperty("tenant").GetProperty("name").GetString().Should().Be("Segunda");
        session.GetProperty("role").GetString().Should().Be("Recruiter");
        session.GetProperty("memberships").GetArrayLength().Should().Be(2);
        CookieValue(response, SessionCookie).Should().NotBe(before);

        using var stale = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        stale.Headers.Add("Cookie", $"{SessionCookie}={before}");
        (await factory.CreateDefaultClient(new Uri("https://localhost")).SendAsync(stale)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "the previous session was revoked");

    }

    private static string SetCookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie").Single(header => header.StartsWith($"{name}=", StringComparison.Ordinal)).ToLowerInvariant();

    private static string CookieValue(HttpResponseMessage response, string name) {
        var header = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith($"{name}=", StringComparison.Ordinal));
        return header[(name.Length + 1)..header.IndexOf(';', StringComparison.Ordinal)];
    }

    /// <summary>La cookie de sesión vigente de un navegador, leída de su próxima petición.</summary>
    private static async Task<string> CurrentCookieAsync(HttpClient browser) {
        var probe = await browser.GetAsync("/api/v1/me");
        probe.RequestMessage!.Headers.TryGetValues("Cookie", out var cookies).Should().BeTrue();
        var pair = string.Join("; ", cookies!).Split("; ").Single(part => part.StartsWith($"{SessionCookie}=", StringComparison.Ordinal));
        return pair[(SessionCookie.Length + 1)..];
    }

}