using System.Net;
using PixApi.Application.Batch;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Api;

// Mesmos cenários do ExportacaoMovimentoIntegrationTest do Java.
public class BatchApiTests(PixApiFactory api) : IClassFixture<PixApiFactory>
{
    private readonly HttpClient _http = api.CreateClient();

    private static readonly DateOnly Hoje =
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ExportacaoMovimentoService.Fuso).DateTime);

    private static decimal Centavos(string digitos) => decimal.Parse(digitos) / 100;

    private async Task<List<string>> Exportar(DateOnly data)
    {
        var resposta = await _http.GetAsync($"/batch/movimento?data={data:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("text/plain", resposta.TipoDeConteudo());
        Assert.Equal($"MOVIMENTO.D{data:yyyyMMdd}", resposta.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.EndsWith("\n", corpo);
        return corpo.TrimEnd('\n').Split('\n').ToList();
    }

    [Fact]
    public async Task Arquivo_DeveTerHeaderContasLancamentosETrailerConsistentes()
    {
        var (a, b) = (HttpExtensions.ChaveUnica("exp"), HttpExtensions.ChaveUnica("exp"));
        await _http.CriarConta(a, "100.00");
        await _http.CriarConta(b, "0.00");
        await _http.PostJson("/transferencias", $$"""{"chaveOrigem": "{{a}}", "chaveDestino": "{{b}}", "valor": 33.33}""");

        var linhas = await Exportar(Hoje);

        Assert.All(linhas, l => { Assert.Equal(150, l.Length); Assert.True(l.All(c => c <= 0x7F)); });
        Assert.StartsWith("H" + Hoje.ToString("yyyyMMdd"), linhas[0]);
        var trailer = linhas[^1];
        Assert.StartsWith("T", trailer);

        var contas = linhas.Where(l => l.StartsWith('C')).ToList();
        var lancamentos = linhas.Where(l => l.StartsWith('L')).ToList();
        Assert.Equal(contas.Count, int.Parse(trailer[1..10]));
        Assert.Equal(lancamentos.Count, int.Parse(trailer[10..19]));

        var creditos = lancamentos.Where(l => l[25] == 'C').Sum(l => Centavos(l[38..53]));
        var debitos = lancamentos.Where(l => l[25] == 'D').Sum(l => Centavos(l[38..53]));
        Assert.Equal(creditos, Centavos(trailer[19..36]));
        Assert.Equal(debitos, Centavos(trailer[36..53]));

        Assert.Contains(contas, l => l.Contains(a) && l[118..133] == "000000000006667");
        Assert.Contains(lancamentos, l => l.Contains("PIX_ENVIADO") && l[38..53] == "000000000003333");
    }

    [Fact]
    public async Task DataFutura_DeveSerRecusada()
    {
        var resposta = await _http.GetAsync($"/batch/movimento?data={Hoje.AddDays(1):yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("Data de referência inválida", (string)(await resposta.Json())["title"]!);
    }

    [Theory]
    [InlineData("26/10/2026")]
    [InlineData("10/26/2026")] // o formato americano também não: só ISO (AAAA-MM-DD)
    [InlineData("ontem")]
    public async Task DataInvalida_DeveSerRecusada(string data)
    {
        var resposta = await _http.GetAsync($"/batch/movimento?data={Uri.EscapeDataString(data)}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task SemData_DeveUsarOntem()
    {
        var corpo = await _http.GetStringAsync("/batch/movimento");

        Assert.StartsWith("H" + Hoje.AddDays(-1).ToString("yyyyMMdd"), corpo);
    }
}
