using System.Globalization;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using PixApi.Application.Batch;

namespace PixApi.Api.Controllers;

[ApiController]
[Route("batch")]
public class BatchController(ExportacaoMovimentoService exportacao) : ControllerBase
{
    // Arquivo texto de largura fixa para o batch noturno em COBOL. Sem "data", exporta o dia anterior.
    [HttpGet("movimento")]
    [Produces("text/plain")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Movimento([FromQuery] string? data, CancellationToken ct)
    {
        // A data chega como texto e é lida só no formato ISO. Com DateOnly direto no parâmetro, o
        // ASP.NET aceitaria também "10/26/2026" (formato americano da cultura invariante).
        DateOnly referencia;
        if (data is null)
        {
            referencia = exportacao.Ontem();
        }
        else if (!DateOnly.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out referencia))
        {
            throw new DataDeReferenciaInvalidaException($"Data inválida: {data}. Use o formato AAAA-MM-DD.");
        }

        var registros = await exportacao.ExportarAsync(referencia, ct);
        Response.Headers.ContentDisposition = new ContentDisposition
        {
            Inline = true,
            FileName = $"MOVIMENTO.D{referencia:yyyyMMdd}",
        }.ToString();
        // US-ASCII: 1 caractere = 1 byte, como o COBOL espera (o layout já removeu acentos).
        return Content(string.Join("\n", registros) + "\n", "text/plain; charset=us-ascii");
    }
}
