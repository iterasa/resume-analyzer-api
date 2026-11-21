using System.Buffers.Text;
using System.Security.Cryptography;

namespace Iterasa.ResumeAnalyzer.Application.Common;

/// <summary>Tokens públicos: impredecibles y aptos para una URL.</summary>
internal static class PublicTokens {

    /// <summary>128 bits de un CSPRNG en base64url (22 caracteres).</summary>
    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

}