using System.Net;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Api;

public class EventosApiTests(PixApiFactory api) : IClassFixture<PixApiFactory>
{
    private readonly HttpClient _http = api.CreateClient();

    [Fact]
    public async Task TransferenciaConfirmada_AparaceEmEventos_ERecusadaNao()
    {
        var (a, b) = (HttpExtensions.ChaveUnica(), HttpExtensions.ChaveUnica());
        await _http.CriarConta(a, "10");
        await _http.CriarConta(b, "0");

        await _http.PostJson("/transferencias", $$"""{"chaveOrigem": "{{a}}", "chaveDestino": "{{b}}", "valor": 3.21}""");
        await _http.PostJson("/transferencias", $$"""{"chaveOrigem": "{{a}}", "chaveDestino": "{{b}}", "valor": 999}""");

        var resposta = await _http.GetAsync("/eventos");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var meus = (await resposta.Json()).AsArray().Where(e => (string)e!["chaveOrigem"]! == a).ToList();
        var evento = Assert.Single(meus)!;
        Assert.Equal(b, (string)evento["chaveDestino"]!);
        Assert.Equal(3.21m, (decimal)evento["valor"]!);
        Assert.True(Guid.TryParse((string)evento["id"]!, out _));
        Assert.EndsWith("Z", (string)evento["realizadaEm"]!);
    }
}
