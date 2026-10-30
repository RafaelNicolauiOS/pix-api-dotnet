using Microsoft.AspNetCore.Diagnostics;

namespace PixApi.Api.Erros;

// Último recurso (IExceptionHandler, novo no .NET 8): qualquer exceção não tratada vira 500
// em ProblemDetails, sem vazar a mensagem interna para o cliente. O detalhe fica no log.
public sealed class ErroInesperadoHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception erro, CancellationToken ct)
    {
        var problema = Problemas.Criar(contexto, StatusCodes.Status500InternalServerError,
            "Erro inesperado. Consulte os logs do servidor.");
        contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await contexto.Response.WriteAsJsonAsync(problema, (System.Text.Json.JsonSerializerOptions?)null,
            Problemas.TipoDeConteudo, ct);
        return true;
    }
}
