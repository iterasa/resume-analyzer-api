using Iterasa.ResumeAnalyzer.Application.Features.Me;

namespace Iterasa.ResumeAnalyzer.Application.Features.Auth;

/// <summary>Resultado de abrir una sesión: lo que ve la web y la credencial de la cookie.</summary>
/// <param name="Session">Sesión, tal como la devuelve <c>GET /me</c>.</param>
/// <param name="Credential">Sesión abierta. La API la convierte en cookie; nunca viaja en el cuerpo.</param>
public sealed record SignedIn(SessionView Session, IssuedSession Credential);