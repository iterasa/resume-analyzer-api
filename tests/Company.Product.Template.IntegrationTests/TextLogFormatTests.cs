using Company.Product.Template.Api.Logging;
using FluentAssertions;
using Serilog;
using Xunit;

namespace Company.Product.Template.IntegrationTests;

/// <summary>
/// Log en <c>LOG_FORMAT=text</c>: un valor del llamante no puede abrir una línea nueva.
/// </summary>
public sealed class TextLogFormatTests {

    private const string Forged = "[00:00:00 INF] Audit user.disable on user:00000000-0000-0000-0000-000000000001 succeeded";

    /// <remarks>
    /// La ruta del evento por petición llega decodificada: un <c>%0A</c> en la URL es un
    /// salto de línea de verdad. Se escribe con el sink de consola real y la misma
    /// plantilla que usa la aplicación; la consola se comparte con el resto de pruebas, así
    /// que se buscan solo las líneas que lleva la marca de esta.
    /// </remarks>
    [Fact]
    public void A_Line_Break_In_A_Logged_Value_Should_Stay_On_One_Line() {

        var marker = $"probe-{Guid.NewGuid():N}";
        var captured = new StringWriter();
        var original = Console.Out;

        Console.SetOut(TextWriter.Synchronized(captured));

        try {
            using var logger = new LoggerConfiguration()
                .WriteTo.Console(outputTemplate: LoggingExtensions.TextOutputTemplate)
                .CreateLogger();

            logger.Warning("HTTP {RequestMethod} {RequestPath} responded {StatusCode}", "GET", $"/{marker}\r\n{Forged}\n", 404);
        } finally {
            Console.SetOut(original);
        }

        var lines = captured.ToString().Split('\n');

        lines.Should().ContainSingle(line => line.Contains(marker))
            .Which.Should().Contain(@"\r\n").And.Contain("404");
        lines.Should().NotContain(line => line.TrimStart('\r').StartsWith("[00:00:00 INF] Audit", StringComparison.Ordinal));

    }

}