namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.DataProtection;

/// <summary>
/// Fila de <c>data_protection_key</c>: una llave del anillo de ASP.NET Data Protection, en XML
/// cifrado con las llaves de plataforma. Estado de infraestructura, no dominio.
/// </summary>
internal sealed class DataProtectionKeyRecord {

    public Guid Id { get; set; }

    public string? FriendlyName { get; set; }

    public string Xml { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

}