namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>Enlaces públicos que la API entrega.</summary>
internal interface IPublicLinks {

    /// <summary>Formulario público de postulación, o <see langword="null" /> si no hay origen público configurado.</summary>
    string? ApplyUrl(string applyToken);

}