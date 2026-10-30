using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PixApi.Application.Eventos;
using PixApi.Application.Portas;
using PixApi.Domain;
using PixApi.Infrastructure.Mensageria;
using PixApi.Infrastructure.Persistencia;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Mensageria;

// Espião: guarda o que foi publicado, no lugar do adaptador de log.
public sealed class PublicadorEspiao : IPublicadorDeEventos
{
    public ConcurrentQueue<TransferenciaRealizadaEvent> Publicados { get; } = new();

    public Task PublicarAsync(TransferenciaRealizadaEvent evento, CancellationToken ct = default)
    {
        Publicados.Enqueue(evento);
        return Task.CompletedTask;
    }
}

// Razão que falha ao gravar o crédito: força um rollback no MEIO da transferência,
// depois de o débito já ter sido feito em memória.
public sealed class RazaoQueFalha(ILancamentoRepositorio real) : ILancamentoRepositorio
{
    public void Adicionar(Lancamento lancamento)
    {
        if (lancamento.Tipo == TipoLancamento.PIX_RECEBIDO)
        {
            throw new InvalidOperationException("falha simulada ao gravar o razão");
        }
        real.Adicionar(lancamento);
    }

    public Task<IReadOnlyList<Lancamento>> ListarAteAsync(DateTimeOffset corte, CancellationToken ct = default) =>
        real.ListarAteAsync(corte, ct);
}

public class EspiaoFactory : PixApiFactory
{
    public PublicadorEspiao Espiao { get; } = new();

    protected override void TrocarServicos(IServiceCollection servicos)
    {
        // Troca só o adaptador: o decorator que registra no histórico (/eventos) continua no meio.
        servicos.AddKeyedSingleton<IPublicadorDeEventos>(PublicadorComHistorico.ChaveDoAdaptador, Espiao);
        servicos.AddScoped<ILancamentoRepositorio>(sp =>
        {
            var real = new LancamentoRepositorio(sp.GetRequiredService<PixDbContext>());
            return Falhar ? new RazaoQueFalha(real) : real;
        });
    }

    public volatile bool Falhar;
}

// Equivale ao PublicacaoAposCommitTest do Java: o evento só sai depois do COMMIT.
public class PublicacaoAposCommitTests(EspiaoFactory api) : IClassFixture<EspiaoFactory>
{
    private readonly HttpClient _http = api.CreateClient();

    private string Corpo(string a, string b, string valor) =>
        $$"""{"chaveOrigem": "{{a}}", "chaveDestino": "{{b}}", "valor": {{valor}}}""";

    [Fact]
    public async Task TransferenciaConfirmada_PublicaUmEvento()
    {
        var (a, b) = (HttpExtensions.ChaveUnica(), HttpExtensions.ChaveUnica());
        await _http.CriarConta(a, "10");
        await _http.CriarConta(b, "0");

        await _http.PostJson("/transferencias", Corpo(a, b, "2.50"));

        var evento = Assert.Single(api.Espiao.Publicados, e => e.ChaveOrigem == a);
        Assert.Equal((b, 2.50m), (evento.ChaveDestino, evento.Valor));
    }

    [Fact]
    public async Task SaldoInsuficiente_NaoPublicaNada()
    {
        var (a, b) = (HttpExtensions.ChaveUnica(), HttpExtensions.ChaveUnica());
        await _http.CriarConta(a, "1");
        await _http.CriarConta(b, "0");

        await _http.PostJson("/transferencias", Corpo(a, b, "2"));

        Assert.DoesNotContain(api.Espiao.Publicados, e => e.ChaveOrigem == a);
    }

    [Fact]
    public async Task FalhaNoMeioDaTransacao_FazRollback_ENaoPublica()
    {
        var (a, b) = (HttpExtensions.ChaveUnica(), HttpExtensions.ChaveUnica());
        await _http.CriarConta(a, "10");
        await _http.CriarConta(b, "0");

        api.Falhar = true;
        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.PostJson("/transferencias", Corpo(a, b, "4"));
        }
        finally
        {
            api.Falhar = false;
        }

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.TipoDeConteudo());
        Assert.Equal(10m, await _http.Saldo(a)); // o débito em memória não chegou ao banco
        Assert.Equal(0m, await _http.Saldo(b));
        Assert.DoesNotContain(api.Espiao.Publicados, e => e.ChaveOrigem == a);
    }
}
