using System.Net;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Api;

public class CorsTests(PixApiFactory api) : IClassFixture<PixApiFactory>
{
    private async Task<HttpResponseMessage> Preflight(string origem)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Options, "/logs");
        requisicao.Headers.Add("Origin", origem);
        requisicao.Headers.Add("Access-Control-Request-Method", "GET");
        requisicao.Headers.Add("Access-Control-Request-Headers", "X-Admin-Token");
        return await api.CreateClient().SendAsync(requisicao);
    }

    [Theory]
    [InlineData("https://rafaelnicolauios.github.io")]
    [InlineData("http://localhost:8000")]
    [InlineData("http://127.0.0.1:5173")]
    public async Task OrigensConhecidas_SaoLiberadas_ComOCabecalhoDoToken(string origem)
    {
        var resposta = await Preflight(origem);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(origem, resposta.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("X-Admin-Token", string.Join(",", resposta.Headers.GetValues("Access-Control-Allow-Headers")),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://site-malicioso.com")]
    [InlineData("http://localhost.evil.com:8000")]
    public async Task OutrasOrigens_SaoBloqueadas(string origem)
    {
        var resposta = await Preflight(origem);

        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
