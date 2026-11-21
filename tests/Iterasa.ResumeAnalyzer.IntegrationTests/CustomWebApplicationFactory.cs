using Iterasa.ResumeAnalyzer.Domain.Users;
using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

/// <summary>
/// Factory de integración: inyecta variables de entorno ficticias y sustituye PostgreSQL
/// por SQLite en memoria, de modo que la suite no necesita una base de datos real.
/// Siembra un usuario para las pruebas que necesitan uno existente.
/// Una prueba que necesite otra configuración deriva de ella y sobrescribe las opciones
/// en el contenedor (<c>PostConfigure</c>), nunca las variables de entorno: son globales
/// al proceso y afectarían a las factories que arrancan en paralelo.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program> {

    /// <summary>Usuario sembrado al arrancar.</summary>
    public const string SeededUsername = "integration-user";

    private SqliteConnection? connection;

    public Guid SeededUserId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder) {

        Environment.SetEnvironmentVariable("DATABASE_CONNECTION_STRING", "Host=127.0.0.1;Port=1;Database=test_db;Username=test;Password=test;Timeout=2");
        Environment.SetEnvironmentVariable("LOG_LEVEL", "Warning");
        Environment.SetEnvironmentVariable("SWAGGER_ENABLED", "false");

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

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

        });

    }

    protected override IHost CreateHost(IHostBuilder builder) {

        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        dbContext.Database.EnsureCreated();

        var seeded = User.Create(new UserCreationArgs(Guid.NewGuid(), SeededUsername, "user@example.test", Guid.Empty), now);

        dbContext.Users.Add(seeded);
        dbContext.SaveChanges();

        SeededUserId = seeded.Id;

        return host;

    }

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (disposing) {
            connection?.Dispose();
            connection = null;
        }
    }

}