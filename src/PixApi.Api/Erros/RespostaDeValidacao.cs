using Microsoft.AspNetCore.Mvc;

namespace PixApi.Api.Erros;

// Formato do 400 de validação igual ao do pix-api:
//   { "title": "Bad Request", "status": 400, "detail": "Dados inválidos", "instance": "/contas",
//     "erros": { "cpf": "deve conter 11 dígitos", ... } }
// O padrão do ASP.NET seria "errors": { "Cpf": ["..."] } — mudaria o contrato com o front-end.
public static class RespostaDeValidacao
{
    public static IActionResult Criar(ActionContext contexto)
    {
        var erros = new SortedDictionary<string, string>(StringComparer.Ordinal); // ordenado, como o TreeMap
        foreach (var (chave, entrada) in contexto.ModelState)
        {
            var primeiro = entrada.Errors.FirstOrDefault();
            if (primeiro is null) continue;
            var (campo, mensagem) = Traduzir(chave, primeiro.ErrorMessage);
            erros.TryAdd(campo, mensagem);
        }
        var problema = Problemas.Criar(contexto.HttpContext, StatusCodes.Status400BadRequest, "Dados inválidos");
        problema.Extensions["erros"] = erros;
        return Problemas.Resultado(problema);
    }

    // Chaves que vêm do leitor de JSON ("$", "$.saldo", "request") têm mensagens em inglês e técnicas.
    private static (string Campo, string Mensagem) Traduzir(string chave, string mensagem) => chave switch
    {
        "$" or "" or "request" => ("corpo", "JSON inválido ou ausente"),
        _ when chave.StartsWith("$.") => (chave[2..], "valor inválido"),
        _ => (chave, mensagem),
    };
}
