using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace PixApi.Api.Erros;

// Monta respostas RFC 9457 (application/problem+json) no mesmo formato do ProblemDetail do Spring:
// title, status, detail, instance (+ extensões como "erros"). Sem "type" (= about:blank) e sem
// "traceId", que o ASP.NET adicionaria por padrão: o contrato com o front-end fica idêntico.
public static class Problemas
{
    public const string TipoDeConteudo = "application/problem+json";

    public static ProblemDetails Criar(HttpContext contexto, int status, string detalhe, string? titulo = null) => new()
    {
        Status = status,
        Title = titulo ?? ReasonPhrases.GetReasonPhrase(status), // "Not Found", "Conflict"...
        Detail = detalhe,
        Instance = contexto.Request.Path,
    };

    public static ObjectResult Resultado(ProblemDetails problema) => new(problema)
    {
        StatusCode = problema.Status,
        ContentTypes = { TipoDeConteudo },
    };

    public static ObjectResult Resultado(HttpContext contexto, int status, string detalhe, string? titulo = null) =>
        Resultado(Criar(contexto, status, detalhe, titulo));
}
