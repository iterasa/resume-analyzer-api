using Microsoft.AspNetCore.Mvc;

namespace Iterasa.ResumeAnalyzer.Api.Idempotency;

/// <summary>
/// Hace idempotente una acción no segura (<c>POST</c>) mediante la cabecera <c>Idempotency-Key</c>.
/// </summary>
/// <remarks>
/// La cabecera es opcional: sin ella la acción se comporta como siempre. Con ella, la
/// primera petición se ejecuta y su respuesta queda registrada; un reintento con la misma
/// clave recibe esa misma respuesta —código, cuerpo y <c>Location</c>— sin ejecutar nada.
/// Ver <see cref="IdempotencyFilter" />.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class IdempotentAttribute() : ServiceFilterAttribute(typeof(IdempotencyFilter)) {
}