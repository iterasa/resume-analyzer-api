using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Configuración del cifrado de campos (variables <c>FIELD_ENCRYPTION_*</c>).
/// </summary>
internal sealed class FieldEncryptionOptions {

    /// <summary>KEK en AWS KMS.</summary>
    public const string AwsKmsProvider = "aws-kms";

    /// <summary>KEK local, solo para desarrollo y pruebas.</summary>
    public const string LocalProvider = "local";

    /// <summary><c>aws-kms</c> o <c>local</c>. Obligatoria: no hay valor por defecto.</summary>
    public string? KekProvider { get; set; }

    /// <summary>Id, ARN o alias de la llave de KMS.</summary>
    public string? KmsKeyId { get; set; }

    /// <summary>KEK local: 32 bytes en base64.</summary>
    public string? LocalKek { get; set; }

}

/// <summary>
/// Valida la configuración al arrancar. Sin KEK no hay arranque, y fuera de desarrollo y
/// pruebas la única KEK admitida es la de KMS: nunca una llave por defecto ni un cifrador nulo.
/// </summary>
internal sealed class FieldEncryptionOptionsValidator(IHostEnvironment environment) : IValidateOptions<FieldEncryptionOptions> {

    public ValidateOptionsResult Validate(string? name, FieldEncryptionOptions options) {

        switch (options.KekProvider) {

            case FieldEncryptionOptions.AwsKmsProvider:
                return string.IsNullOrWhiteSpace(options.KmsKeyId)
                    ? ValidateOptionsResult.Fail("FIELD_ENCRYPTION_KMS_KEY_ID is required when FIELD_ENCRYPTION_KEK_PROVIDER is aws-kms.")
                    : ValidateOptionsResult.Success;

            case FieldEncryptionOptions.LocalProvider:
                if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing")) {
                    return ValidateOptionsResult.Fail(
                        $"FIELD_ENCRYPTION_KEK_PROVIDER=local is only allowed in Development or Testing (current: {environment.EnvironmentName}). Use aws-kms.");
                }
                return IsLocalKek(options.LocalKek)
                    ? ValidateOptionsResult.Success
                    : ValidateOptionsResult.Fail("FIELD_ENCRYPTION_LOCAL_KEK must be 32 bytes encoded in base64.");

            default:
                return ValidateOptionsResult.Fail(
                    $"FIELD_ENCRYPTION_KEK_PROVIDER must be '{FieldEncryptionOptions.AwsKmsProvider}' or '{FieldEncryptionOptions.LocalProvider}'.");

        }

    }

    internal static bool IsLocalKek(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }
        var buffer = new byte[48];
        return Convert.TryFromBase64String(value.Trim(), buffer, out var written) && written == 32;
    }

}