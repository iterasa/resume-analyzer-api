using System.Security.Cryptography;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

/// <summary>
/// Llave maestra local de las pruebas: aleatoria por proceso, nunca una constante en el
/// repositorio.
/// </summary>
internal static class TestKeys {

    public static readonly string LocalKek = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

}