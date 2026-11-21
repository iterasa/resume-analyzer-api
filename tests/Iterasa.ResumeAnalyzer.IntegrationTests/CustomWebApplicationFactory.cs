using Iterasa.ResumeAnalyzer.Api.RateLimiting;
using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.Passwords;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

/// <summary>
/// Factory de integración: inyecta variables de entorno ficticias y sustituye PostgreSQL por
/// SQLite en memoria, de modo que la suite no necesita una base de datos real.
/// </summary>
/// <remarks>
/// Las pruebas usan sesiones de verdad: registran una empresa por la API y siguen con su cookie,
/// como la web (<see cref="CreateBrowser" />). Una prueba que necesite otra configuración deriva
/// de ella y sobrescribe las opciones en el contenedor (<c>PostConfigure</c>), nunca las variables
/// de entorno: son globales al proceso y afectarían a las factories que arrancan en paralelo.
/// </remarks>
public class CustomWebApplicationFactory : WebApplicationFactory<Program> {

    /// <summary>Contraseña de todas las cuentas de prueba.</summary>
    public const string Password = "correct horse battery staple";

    private SqliteConnection? connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder) {

        Environment.SetEnvironmentVariable("DATABASE_CONNECTION_STRING", "Host=127.0.0.1;Port=1;Database=test_db;Username=test;Password=test;Timeout=2");
        Environment.SetEnvironmentVariable("LOG_LEVEL", "Warning");
        Environment.SetEnvironmentVariable("SWAGGER_ENABLED", "false");
        // Llave maestra local: el entorno «Testing» la admite; producción no arranca con ella.
        Environment.SetEnvironmentVariable("FIELD_ENCRYPTION_KEK_PROVIDER", "local");
        Environment.SetEnvironmentVariable("FIELD_ENCRYPTION_LOCAL_KEK", TestKeys.LocalKek);
        Environment.SetEnvironmentVariable("PURGE_INTERVAL_MINUTES", "0");

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services => {

            foreach (var registration in services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                || d.ServiceType == typeof(DbContextOptions)
                || d.ServiceType == typeof(AppDbContext)
                || d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal)).ToList()) {
                services.Remove(registration);
            }

            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            // Cada factory es una aplicación con su propio cifrado de campos: EF crea un proveedor
            // interno por cada una, y en la suite completa pasan de veinte (ver PostgresTestDatabase).
            services.AddDbContext<AppDbContext>(options => options
                .UseSqlite(connection)
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));

            // Todas las peticiones de la suite salen de la misma «IP»: el límite de inicio de
            // sesión se prueba aparte (RateLimitTests). Argon2id con el mínimo admitido, por velocidad.
            services.PostConfigure<RateLimitingOptions>(options => {
                options.PermitLimit = 10_000;
                options.AuthPermitLimit = 10_000;
            });
            services.PostConfigure<PasswordHashingOptions>(options => {
                options.Argon2MemoryKib = PasswordHashingOptions.MinimumArgon2MemoryKib;
                options.Argon2Iterations = 1;
            });

        });

    }

    /// <summary>
    /// Un «navegador»: guarda cookies, habla HTTPS (las cookies son <c>Secure</c>) y, como la web,
    /// pide el token antiforgery y lo reenvía en cada mutación.
    /// </summary>
    /// <param name="antiforgery">Si envía el token antiforgery.</param>
    public HttpClient CreateBrowser(bool antiforgery = true) {
        var cookies = new CookieContainer();
        var client = CreateDefaultClient(
            new Uri("https://localhost"),
            new AntiforgeryHandler(cookies, antiforgery),
            new CookieContainerHandler(cookies));
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }

    /// <summary>Un cliente sin sesión.</summary>
    public HttpClient CreateAnonymousClient() => CreateBrowser();

    /// <summary>Registra una empresa nueva y devuelve el navegador con su sesión de administrador.</summary>
    public async Task<SignedIn> RegisterAsync(string? email = null, string companyName = "Acme") {

        var browser = CreateBrowser();
        email ??= $"admin-{Guid.NewGuid():N}@example.test";

        var response = await browser.PostAsJsonAsync("/api/v1/auth/register", new {
            companyName,
            country = "Guatemala",
            name = "Ana Admin",
            email,
            password = Password,
            acceptTerms = true,
        });

        if (response.StatusCode != HttpStatusCode.Created) {
            throw new InvalidOperationException($"Register failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        var session = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new SignedIn(
            browser,
            Guid.Parse(session.GetProperty("user").GetProperty("id").GetString()!),
            Guid.Parse(session.GetProperty("tenant").GetProperty("id").GetString()!),
            email);

    }

    /// <summary>Ejecuta algo con los servicios de la aplicación (sembrar o inspeccionar la base).</summary>
    public async Task InScopeAsync(Func<IServiceProvider, Task> action) {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    protected override IHost CreateHost(IHostBuilder builder) {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (disposing) {
            connection?.Dispose();
            connection = null;
        }
    }

    /// <summary>Sesión abierta por <see cref="RegisterAsync" />.</summary>
    public sealed record SignedIn(HttpClient Browser, Guid UserId, Guid TenantId, string Email);

    /// <summary>Como la web: si falta la cookie <c>XSRF-TOKEN</c>, la pide; y la copia en <c>X-XSRF-TOKEN</c>.</summary>
    private sealed class AntiforgeryHandler(CookieContainer cookies, bool enabled) : DelegatingHandler {

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {

            if (enabled && request.Method != HttpMethod.Get && request.Method != HttpMethod.Head && request.Method != HttpMethod.Options) {

                var token = Token(request.RequestUri!);

                if (token is null) {
                    using var bootstrap = new HttpRequestMessage(HttpMethod.Get, new Uri(request.RequestUri!, "/api/v1/antiforgery/token"));
                    (await base.SendAsync(bootstrap, cancellationToken)).Dispose();
                    token = Token(request.RequestUri!);
                }

                request.Headers.Remove("X-XSRF-TOKEN");
                request.Headers.Add("X-XSRF-TOKEN", token);

            }

            return await base.SendAsync(request, cancellationToken);

        }

        private string? Token(Uri uri) => cookies.GetCookies(uri)["XSRF-TOKEN"]?.Value;

    }

}