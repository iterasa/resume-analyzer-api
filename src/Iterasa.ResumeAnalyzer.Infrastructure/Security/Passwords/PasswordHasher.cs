using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.Passwords;

/// <summary>
/// Hash de contraseñas: Argon2id por defecto, PBKDF2-HMAC-SHA256 para FIPS.
/// </summary>
/// <remarks>
/// Todo hash se serializa con su algoritmo y sus parámetros (Argon2id en el formato PHC
/// estándar, <c>$argon2id$v=19$m=…,t=…,p=…$sal$hash</c>), de modo que la verificación no
/// depende de la configuración vigente y cambiar de algoritmo o endurecer parámetros no
/// invalida nada: <see cref="NeedsRehash" /> señala los hashes antiguos y el inicio de
/// sesión los regenera. La verificación gasta el mismo tiempo exista o no el hash
/// almacenado: una contraseña de relleno se hashea con el algoritmo configurado.
/// </remarks>
internal sealed class PasswordHasher : IPasswordHasher {

    private const int SaltSize = 16;
    private const int KeySize = 32;

    private readonly PasswordHashingOptions options;
    private readonly string dummyHash;

    public PasswordHasher(IOptions<PasswordHashingOptions> options) {
        this.options = options.Value;
        dummyHash = Hash(Guid.NewGuid().ToString());
    }

    /// <inheritdoc />
    public string Hash(string password) => options.Algorithm switch {
        PasswordHashAlgorithm.Pbkdf2Sha256 => Pbkdf2Sha256.Hash(password, Pbkdf2Sha256.Iterations),
        _ => Argon2id.Hash(password, options.Argon2MemoryKib, options.Argon2Iterations, options.Argon2Parallelism)
    };

    /// <inheritdoc />
    public bool Verify(string password, string? passwordHash) {

        var hasStoredHash = !string.IsNullOrWhiteSpace(passwordHash);
        var matches = Matches(password, hasStoredHash ? passwordHash! : dummyHash);

        return hasStoredHash && matches;

    }

    /// <inheritdoc />
    public bool NeedsRehash(string passwordHash) => options.Algorithm switch {
        PasswordHashAlgorithm.Pbkdf2Sha256 => !Pbkdf2Sha256.Matches(passwordHash, Pbkdf2Sha256.Iterations),
        _ => !Argon2id.Matches(passwordHash, options.Argon2MemoryKib, options.Argon2Iterations, options.Argon2Parallelism)
    };

    private static bool Matches(string password, string passwordHash) {

        if (passwordHash.StartsWith(Argon2id.Prefix, StringComparison.Ordinal)) {
            return Argon2id.Verify(password, passwordHash);
        }

        if (passwordHash.StartsWith(Pbkdf2Sha256.Prefix, StringComparison.Ordinal)) {
            return Pbkdf2Sha256.Verify(password, passwordHash);
        }

        return false;

    }

    private static string ToBase64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static bool TryFromBase64(string value, out byte[] bytes) {
        try {
            bytes = Convert.FromBase64String(value.PadRight(value.Length + ((4 - (value.Length % 4)) % 4), '='));
            return true;
        } catch (FormatException) {
            bytes = [];
            return false;
        }
    }

    /// <summary>Argon2id en formato PHC: <c>$argon2id$v=19$m=19456,t=2,p=1$sal$hash</c>.</summary>
    internal static class Argon2id {

        public const string Prefix = "$argon2id$";
        private const int Version = 19;

        public static string Hash(string password, int memoryKib, int iterations, int parallelism) {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Derive(password, salt, memoryKib, iterations, parallelism);
            return $"{Prefix}v={Version}$m={memoryKib},t={iterations},p={parallelism}${ToBase64(salt)}${ToBase64(key)}";
        }

        public static bool Verify(string password, string passwordHash) {

            if (!TryParse(passwordHash, out var memoryKib, out var iterations, out var parallelism, out var salt, out var key)) {
                return false;
            }

            var candidate = Derive(password, salt, memoryKib, iterations, parallelism);
            return CryptographicOperations.FixedTimeEquals(candidate, key);

        }

        /// <summary>Indica si el hash ya está en el algoritmo y con los parámetros indicados.</summary>
        public static bool Matches(string passwordHash, int memoryKib, int iterations, int parallelism) =>
            TryParse(passwordHash, out var m, out var t, out var p, out _, out _) && m == memoryKib && t == iterations && p == parallelism;

        private static byte[] Derive(string password, byte[] salt, int memoryKib, int iterations, int parallelism) {
            using var argon2 = new Konscious.Security.Cryptography.Argon2id(Encoding.UTF8.GetBytes(password)) {
                Salt = salt,
                MemorySize = memoryKib,
                Iterations = iterations,
                DegreeOfParallelism = parallelism
            };
            return argon2.GetBytes(KeySize);
        }

        private static bool TryParse(string passwordHash, out int memoryKib, out int iterations, out int parallelism, out byte[] salt, out byte[] key) {

            memoryKib = iterations = parallelism = 0;
            salt = key = [];

            // $argon2id$v=19$m=19456,t=2,p=1$<salt>$<hash>  → ["", "argon2id", "v=19", "m=…,t=…,p=…", salt, hash]
            var parts = passwordHash.Split('$');

            if (parts.Length != 6 || parts[1] != "argon2id" || parts[2] != $"v={Version}") {
                return false;
            }

            var parameters = parts[3].Split(',');

            if (parameters.Length != 3
                || !TryParseParameter(parameters[0], "m", out memoryKib)
                || !TryParseParameter(parameters[1], "t", out iterations)
                || !TryParseParameter(parameters[2], "p", out parallelism)
                || memoryKib <= 0 || iterations <= 0 || parallelism <= 0) {
                return false;
            }

            return TryFromBase64(parts[4], out salt) && TryFromBase64(parts[5], out key) && salt.Length > 0 && key.Length > 0;

        }

        private static bool TryParseParameter(string raw, string name, out int value) {
            value = 0;
            return raw.StartsWith($"{name}=", StringComparison.Ordinal)
                && int.TryParse(raw.AsSpan(name.Length + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

    }

    /// <summary>PBKDF2-HMAC-SHA256: <c>pbkdf2-sha256$600000$sal$clave</c>. Aprobado por FIPS-140.</summary>
    internal static class Pbkdf2Sha256 {

        public const string Prefix = "pbkdf2-sha256$";

        /// <summary>Mínimo que fija OWASP para PBKDF2-HMAC-SHA256.</summary>
        public const int Iterations = 600_000;

        public static string Hash(string password, int iterations) {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
            return $"{Prefix}{iterations.ToString(CultureInfo.InvariantCulture)}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        public static bool Verify(string password, string passwordHash) {

            if (!TryParse(passwordHash, out var iterations, out var salt, out var key)) {
                return false;
            }

            var candidate = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, key.Length);
            return CryptographicOperations.FixedTimeEquals(candidate, key);

        }

        public static bool Matches(string passwordHash, int iterations) =>
            TryParse(passwordHash, out var stored, out _, out _) && stored == iterations;

        private static bool TryParse(string passwordHash, out int iterations, out byte[] salt, out byte[] key) {

            iterations = 0;
            salt = key = [];

            var parts = passwordHash.Split('$');

            return parts.Length == 4
                && parts[0] == "pbkdf2-sha256"
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out iterations)
                && iterations > 0
                && TryFromBase64(parts[2], out salt)
                && TryFromBase64(parts[3], out key)
                && salt.Length > 0 && key.Length > 0;

        }

    }

}