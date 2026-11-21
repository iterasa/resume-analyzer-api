using System.Text.Json.Serialization;

namespace Iterasa.ResumeAnalyzer.Application.Features.Auth;

/// <summary>Sesión recién abierta: lo que la API necesita para emitir la cookie.</summary>
/// <param name="SessionId">Identificador de la sesión.</param>
/// <param name="Token">Token opaco de la cookie. La base solo guarda su hash, y nunca se serializa.</param>
/// <param name="ExpiresAtUtc">Vencimiento absoluto.</param>
/// <param name="Persistent">Si la cookie debe sobrevivir al cierre del navegador («Recordarme»).</param>
public sealed record IssuedSession(Guid SessionId, [property: JsonIgnore] string Token, DateTimeOffset ExpiresAtUtc, bool Persistent) {

    /// <summary>El token no aparece al imprimir el registro.</summary>
    public override string ToString() => $"IssuedSession {{ SessionId = {SessionId}, ExpiresAtUtc = {ExpiresAtUtc:O}, Persistent = {Persistent} }}";

}