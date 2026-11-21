using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.Sessions;

/// <summary>Tokens de sesión: 256 bits de un CSPRNG, en base64url. La base guarda su SHA-256.</summary>
/// <remarks>
/// Un hash rápido basta: el token es aleatorio y largo, no hay nada que adivinar por fuerza bruta
/// (a diferencia de una contraseña).
/// </remarks>
internal static class SessionTokens {

    public const int TokenSize = 32;
    public const int HashSize = 32;

    /// <summary>Longitud del token en base64url.</summary>
    public const int EncodedLength = 43;

    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSize));

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.ASCII.GetBytes(token));

    /// <summary>Forma válida: evita consultar la base con cualquier cosa que llegue en una cookie.</summary>
    public static bool IsWellFormed(string? token) =>
        token is { Length: EncodedLength } && Base64Url.IsValid(token, out var decoded) && decoded == TokenSize;

}