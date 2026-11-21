using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>Enlaces públicos a partir de <c>APP_PUBLIC_URL</c>.</summary>
internal sealed class PublicLinks(IOptions<PublicLinksOptions> options) : IPublicLinks {

    /// <inheritdoc />
    public string? ApplyUrl(string applyToken) =>
        options.Value.AppPublicUrl is { } origin ? $"{origin}/postular/{Uri.EscapeDataString(applyToken)}" : null;

}

/// <summary>Origen público de la web, sin barra final.</summary>
internal sealed class PublicLinksOptions {

    public string? AppPublicUrl { get; set; }

}