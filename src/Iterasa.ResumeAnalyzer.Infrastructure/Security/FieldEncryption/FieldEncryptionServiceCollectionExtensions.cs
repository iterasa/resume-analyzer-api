using Amazon.KeyManagementService;
using Iterasa.ResumeAnalyzer.Shared.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Registro del cifrado de campos.
/// </summary>
internal static class FieldEncryptionServiceCollectionExtensions {

    /// <summary>Lee <c>FIELD_ENCRYPTION_*</c>, lo valida al arrancar y registra los servicios.</summary>
    public static IServiceCollection AddFieldEncryption(this IServiceCollection services) {

        services
            .AddOptions<FieldEncryptionOptions>()
            .Configure(options => {
                options.KekProvider = EnvironmentVariableReader.GetOptional(EnvironmentVariables.FieldEncryptionKekProvider)?.Trim().ToLowerInvariant();
                options.KmsKeyId = EnvironmentVariableReader.GetOptional(EnvironmentVariables.FieldEncryptionKmsKeyId)?.Trim();
                options.LocalKek = EnvironmentVariableReader.GetOptional(EnvironmentVariables.FieldEncryptionLocalKek)?.Trim();
            })
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<FieldEncryptionOptions>, FieldEncryptionOptionsValidator>();

        return services.AddFieldEncryptionCore();

    }

    /// <summary>Los servicios, con las opciones ya configuradas por quien llama (pruebas).</summary>
    public static IServiceCollection AddFieldEncryptionCore(this IServiceCollection services) {

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IKeyEncryptionKeyProvider>(provider => {
            var options = provider.GetRequiredService<IOptions<FieldEncryptionOptions>>().Value;
            return options.KekProvider switch {
                FieldEncryptionOptions.AwsKmsProvider => new AwsKmsKeyEncryptionKeyProvider(
                    new AmazonKeyManagementServiceClient(), options.KmsKeyId!),
                FieldEncryptionOptions.LocalProvider => new LocalKeyEncryptionKeyProvider(
                    Convert.FromBase64String(options.LocalKek!)),
                _ => throw new InvalidOperationException("Field encryption has no key encryption key configured."),
            };
        });

        services.AddSingleton<EncryptionKeyProvisioner>();
        services.AddSingleton<IKeyRingSource, DatabaseKeyRingSource>();
        services.AddSingleton<KeyRingCache>();
        services.AddSingleton<FieldCipher>();
        services.AddSingleton<BlindIndexer>();
        services.AddSingleton<EfFieldEncryption>();
        services.AddScoped<EncryptionKeyRotation>();

        return services;

    }

}