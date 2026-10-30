using System.Net;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Api;

public class TransferenciasApiTests(PixApiFactory api) : IClassFixture<PixApiFactory>
{
    private readonly HttpClient _http = api.CreateClient();

    private static string Corpo(string origem, string destino, string valor) =>
        $$"""{"chaveOrigem": "{{origem}}", "chaveDestino": "{{destino}}", "valor": {{valor}}}""";

    [Fact]
    public async Task Post_DeveTransferir_GravarNoBanco_ERetornarComprovante()
    {
        var (a, b) = (HttpExtensions.ChaveUnica(), HttpExtensions.ChaveUnica());
        await _http.CriarConta(a, "100.00");
        await _http.CriarConta(b, "0.00");

        var resposta = await _http.PostJson("/transferencias", Corpo(a, b, "30.50"));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Json();
        Assert.Equal(a, (string)corpo["chaveOrigem"]!);
        Assert.Equal(b, (string)corpo["chaveDestino"]!);
        Assert.Equal(30.50m, (decimal)corpo["valor"]!);
        Assert.Equal(69.50m, (decimal)corpo["saldoOrigem"]!);
        // Mesmo formato do Instant do Java: UTC com "Z", e não "+00:00".
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?Z$", (string)corpo["realizadaEm"]!);

        Assert.Equal(69.50m, await _http.Saldo(a));
        Assert.Equal(30.50m, await _http.Saldo(b));
    }

    [Fact]
    public async Task Post_SemSaldo_DeveRetornar422ProblemDetails_ENadaMudar()
    {
        var (a, b) = (HttpExtensions.ChaveUnica(), HttpExtensions.ChaveUnica());
        await _http.CriarConta(a, "50.00");
        await _http.CriarConta(b, "0");

        var resposta = await _http.PostJson("/transferencias", Corpo(a, b, "50.01"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.TipoDeConteudo());
        var corpo = await resposta.Json();
        Assert.Equal("Saldo insuficiente", (string)corpo["title"]!);
        Assert.Equal(422, (int)corpo["status"]!);
        Assert.Equal($"Saldo insuficiente na chave {a} para transferir R$ 50.01", (string)corpo["detail"]!);
        Assert.Equal("/transferencias", (string)corpo["instance"]!);
        Assert.Equal(50.00m, await _http.Saldo(a));
    }

    [Fact]
    public async Task Post_ChaveInexistente_DeveRetornar404()
    {
        var a = HttpExtensions.ChaveUnica();
        await _http.CriarConta(a, "10");

        var resposta = await _http.PostJson("/transferencias", Corpo(a, "ninguem@pix.com", "1"));

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        var corpo = await resposta.Json();
        Assert.Equal("Chave Pix não encontrada", (string)corpo["title"]!);
        Assert.Equal("Chave Pix não encontrada: ninguem@pix.com", (string)corpo["detail"]!);
    }

    [Fact]
    public async Task Post_OrigemIgualDestino_DeveRetornar422()
    {
        var resposta = await _http.PostJson("/transferencias", Corpo("ana@pix.com", "ana@pix.com", "1"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("Transferência inválida", (string)(await resposta.Json())["title"]!);
    }

    [Theory]
    [InlineData("0.001")] // mais de 2 casas
    [InlineData("0")]
    [InlineData("-1")]
    public async Task Post_ValorInvalido_DeveRetornar400(string valor)
    {
        var resposta = await _http.PostJson("/transferencias", Corpo("", "bruno@pix.com", valor));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await resposta.Json())["erros"]!;
        Assert.NotNull(erros["chaveOrigem"]);
        Assert.NotNull(erros["valor"]);
    }
}
