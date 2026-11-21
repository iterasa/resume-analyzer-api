using FluentAssertions;
using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Iterasa.ResumeAnalyzer.IntegrationTests.FieldEncryption;

/// <summary>
/// El recorrido completo contra PostgreSQL real, con las migraciones: SQLite comparte una sola
/// conexión y no ve lo que ve PostgreSQL entre transacciones (las llaves de una empresa que se
/// está registrando, <c>xmin</c>, <c>date</c>, <c>RETURNING</c>).
/// </summary>
public sealed class PostgresEndToEndTests {

    [Fact]
    public async Task Register_Sign_In_And_Manage_A_Vacancy_On_PostgreSQL() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var factory = new PostgresFactory(database.ConnectionString);

        var admin = await factory.RegisterAsync(companyName: "Altamira");

        var vacancy = await VacancyEndpointTests.CreateAsync(admin);
        var id = vacancy.GetProperty("id").GetString();
        var requirement = vacancy.GetProperty("requirements")[0].GetProperty("id").GetString();

        var updated = await admin.Browser.PutAsJsonAsync($"/api/v1/vacancies/{id}/requirements", new {
            requirements = new object[] { new { id = requirement, text = "C# avanzado", kind = "Mandatory", weight = 5 } },
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());

        (await admin.Browser.PostAsync($"/api/v1/vacancies/{id}/pause", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.Browser.GetFromJsonAsync<JsonElement>("/api/v1/vacancies?status=Paused")).GetProperty("totalCount").GetInt32().Should().Be(1);
        (await admin.Browser.GetFromJsonAsync<JsonElement>("/api/v1/vacancies/owner-options")).GetArrayLength().Should().Be(1);

        var other = factory.CreateBrowser();
        (await other.PostAsJsonAsync("/api/v1/auth/login", new { email = admin.Email, password = CustomWebApplicationFactory.Password, rememberMe = false }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await other.PostAsync("/api/v1/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await other.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await admin.Browser.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.OK, "logging out one session leaves the others alone");

        var stored = await database.QueryAsync("SELECT count(*) FROM data_protection_key WHERE xml LIKE 'enc1.%'");
        ((long)stored.Single()[0]!).Should().BeGreaterThan(0, "the antiforgery keys are stored encrypted");

    }

    private sealed class PostgresFactory(string connectionString) : CustomWebApplicationFactory {

        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => {
                foreach (var registration in services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || d.ServiceType == typeof(DbContextOptions)
                    || d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal)).ToList()) {
                    services.Remove(registration);
                }
                services.AddDbContext<AppDbContext>(options => options
                    .UseNpgsql(connectionString)
                    .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
            });
        }

        /// <remarks>La base ya está migrada: no se crea con <c>EnsureCreated</c>.</remarks>
        protected override IHost CreateHost(IHostBuilder builder) => builder.Build().StartAndReturn();

    }

}

internal static class HostExtensions {

    public static IHost StartAndReturn(this IHost host) {
        host.Start();
        return host;
    }

}