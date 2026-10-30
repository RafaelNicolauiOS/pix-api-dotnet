using Microsoft.AspNetCore.Mvc.Filters;
using PixApi.Application.Batch;
using PixApi.Application.Contas;
using PixApi.Application.Transferencias;
using PixApi.Domain;

namespace PixApi.Api.Erros;

// Converte exceções de negócio em ProblemDetails: o @RestControllerAdvice do Java.
// É um filtro do MVC (roda só em volta dos controllers); o que ele não conhece segue para o
// ErroInesperadoHandler, que responde 500.
public sealed class ApiExceptionFilter(ILogger<ApiExceptionFilter> log) : IExceptionFilter
{
    public void OnException(ExceptionContext contexto)
    {
        var http = contexto.HttpContext;
        (int Status, string? Titulo)? mapeado = contexto.Exception switch
        {
            ContaNaoEncontradaException => (StatusCodes.Status404NotFound, null),
            ChavePixNaoEncontradaException => (StatusCodes.Status404NotFound, "Chave Pix não encontrada"),
            ChavePixDuplicadaException => (StatusCodes.Status409Conflict, null),
            // 422: a requisição está bem formada, mas viola uma regra de negócio.
            SaldoInsuficienteException => (StatusCodes.Status422UnprocessableEntity, "Saldo insuficiente"),
            TransferenciaInvalidaException => (StatusCodes.Status422UnprocessableEntity, "Transferência inválida"),
            DataDeReferenciaInvalidaException => (StatusCodes.Status400BadRequest, "Data de referência inválida"),
            _ => null,
        };
        if (mapeado is not { } m)
        {
            return;
        }
        // Erro esperado: uma linha de INFO basta (sem stack trace), e aparece na página Atividade.
        log.LogInformation("{Metodo} {Caminho} → {Status}: {Mensagem}", http.Request.Method, http.Request.Path,
            m.Status, contexto.Exception.Message);
        contexto.Result = Problemas.Resultado(http, m.Status, contexto.Exception.Message, m.Titulo);
        contexto.ExceptionHandled = true;
    }
}
