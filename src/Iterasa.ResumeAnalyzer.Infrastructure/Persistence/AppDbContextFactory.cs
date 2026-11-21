using Iterasa.ResumeAnalyzer.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <remarks>
/// Permite que las herramientas de EF Core (<c>dotnet ef</c>) construyan el contexto sin
/// arrancar la aplicación web, de modo que la capa de API no necesita conocer EF Core.
/// Requiere la variable <c>DATABASE_CONNECTION_STRING</c>.
/// </remarks>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext> {

    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args) {

        var connectionString = EnvironmentVariableReader.GetRequired(EnvironmentVariables.DatabaseConnectionString);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);

    }

}