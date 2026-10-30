using System.Data;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PixApi.Application.Batch;
using PixApi.Application.Portas;
using PixApi.Domain;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Application;

public class ExportacaoMovimentoServiceTests
{
    // 25/10/2026 02:30 em Brasília = 05:30 UTC.
    private static readonly DateTimeOffset Agora = new(2026, 10, 25, 5, 30, 0, TimeSpan.Zero);

    private readonly Mock<IContaRepositorio> _contas = new();
    private readonly Mock<ILancamentoRepositorio> _lancamentos = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new([]);
    private readonly ExportacaoMovimentoService _service;

    public ExportacaoMovimentoServiceTests()
    {
        _service = new ExportacaoMovimentoService(_contas.Object, _lancamentos.Object, _uow, new FakeTimeProvider(Agora));
    }

    [Fact]
    public void Ontem_UsaODiaDeBrasilia_ENaoOUtc()
    {
        Assert.Equal(new DateOnly(2026, 10, 24), _service.Ontem());
    }

    [Fact]
    public async Task DataFutura_DeveSerRecusada()
    {
        await Assert.ThrowsAsync<DataDeReferenciaInvalidaException>(() =>
            _service.ExportarAsync(new DateOnly(2026, 10, 26)));
    }

    [Fact]
    public async Task Exportar_GeraHeaderContasLancamentosETrailer_NumaUnicaFotoDoBanco()
    {
        var id = Guid.NewGuid();
        _contas.Setup(r => r.ListarOrdenadoPorIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            [Fabrica.Conta("ana@pix.com", 70m, 1), Fabrica.Conta("bruno@pix.com", 30m, 2)]);
        _lancamentos.Setup(r => r.ListarAteAsync(Agora, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            Lancamento.Abertura(1, 100m, Agora.AddHours(-30)),
            Lancamento.PixEnviado(1, 30m, Agora.AddHours(-1), id),
            Lancamento.PixRecebido(2, 30m, Agora.AddHours(-1), id),
        ]);

        var linhas = await _service.ExportarAsync(new DateOnly(2026, 10, 24));

        Assert.Equal(IsolationLevel.RepeatableRead, _uow.Isolamento);
        Assert.Equal(['H', 'C', 'C', 'L', 'L', 'L', 'T'], linhas.Select(l => l[0]));
        Assert.StartsWith("H2026102420261025023000PIX-API", linhas[0]); // data/hora de Brasília
        Assert.Equal("L0000000001" + "20261025" + "013000" + "D", linhas[4][..26]);
        // 2 contas, 3 lançamentos, créditos 130,00, débitos 30,00
        Assert.Equal("T000000002000000003" + "00000000000013000" + "00000000000003000", linhas[6][..53]);
    }
}
