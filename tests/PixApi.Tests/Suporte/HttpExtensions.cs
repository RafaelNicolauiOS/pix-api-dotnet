using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace PixApi.Tests.Suporte;

public static class HttpExtensions
{
    public static Task<HttpResponseMessage> PostJson(this HttpClient http, string url, string json) =>
        http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

    public static Task<HttpResponseMessage> PutJson(this HttpClient http, string url, string json) =>
        http.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

    public static async Task<JsonNode> Json(this HttpResponseMessage resposta) =>
        JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!;

    public static string? TipoDeConteudo(this HttpResponseMessage resposta) =>
        resposta.Content.Headers.ContentType?.MediaType;

    public static string ChaveUnica(string prefixo = "t") => $"{prefixo}-{Guid.NewGuid().ToString()[..8]}@pix.com";

    public static async Task CriarConta(this HttpClient http, string chave, string saldo)
    {
        var resposta = await http.PostJson("/contas",
            $$"""{"titular": "Teste", "cpf": "12345678901", "chavePix": "{{chave}}", "saldo": {{saldo}}}""");
        if (resposta.StatusCode != System.Net.HttpStatusCode.Created)
        {
            throw new InvalidOperationException($"criar conta: {resposta.StatusCode} {await resposta.Content.ReadAsStringAsync()}");
        }
    }

    public static async Task<decimal> Saldo(this HttpClient http, string chave)
    {
        var contas = (await (await http.GetAsync("/contas")).Json()).AsArray();
        return contas.Single(c => (string)c!["chavePix"]! == chave)!["saldo"]!.GetValue<decimal>();
    }

    public static MediaTypeHeaderValue ProblemJson => new("application/problem+json");
}
