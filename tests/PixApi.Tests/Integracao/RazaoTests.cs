using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PixApi.Infrastructure.Persistencia;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Integracao;

// Invariante do razão (RazaoIntegrationTest do Java): para cada conta, soma do razão = saldo.
public class RazaoTests(PixApiFactory api) : IClassFixture<PixApiFactory>
{
    private readonly HttpClient _http = api.CreateClient();

    private async Task<decimal> SomaDoRazao(string chave)
    {
        using var escopo = api.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<PixDbContext>();
        var contaId = await db.Contas.Where(c => c.ChavePix == chave).Select(c => c.Id).SingleAsync();
        var lancamentos = await db.Lancamentos.Where(l => l.ContaId == contaId).ToListAsync();
        return lancamentos.Sum(l => l.ValorComSinal());
    }

    [Fact]
    public async Task SomaDoRazao_IgualAoSaldo_DepoisDeTransferenciasERecusas()
    {
        var (a, b) = (HttpExtensions.ChaveUnica(), HttpExtensions.ChaveUnica());
        await _http.CriarConta(a, "100.00");
        await _http.CriarConta(b, "20.00");

        await _http.PostJson("/transferencias", $$"""{"chaveOrigem": "{{a}}", "chaveDestino": "{{b}}", "valor": 33.33}""");
        await _http.PostJson("/transferencias", $$"""{"chaveOrigem": "{{b}}", "chaveDestino": "{{a}}", "valor": 3.33}""");
        await _http.PostJson("/transferencias", $$"""{"chaveOrigem": "{{b}}", "chaveDestino": "{{a}}", "valor": 1000}"""); // 422

        Assert.Equal(await _http.Saldo(a), await SomaDoRazao(a));
        Assert.Equal(await _http.Saldo(b), await SomaDoRazao(b));
        Assert.Equal(70.00m, await _http.Saldo(a));
    }

    [Fact]
    public async Task ContaEncerrada_FicaComRazaoZerado()
    {
        var chave = HttpExtensions.ChaveUnica();
        var id = (long)(await (await _http.PostJson("/contas",
            $$"""{"titular": "X", "cpf": "12345678901", "chavePix": "{{chave}}", "saldo": 42.10}""")).Json())["id"]!;
        await _http.DeleteAsync($"/contas/{id}");

        using var escopo = api.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<PixDbContext>();
        var lancamentos = await db.Lancamentos.Where(l => l.ContaId == id).ToListAsync();
        Assert.Equal(2, lancamentos.Count); // abertura + encerramento: o histórico fica
        Assert.Equal(0m, lancamentos.Sum(l => l.ValorComSinal()));
    }
}
