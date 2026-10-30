using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PixApi.Application.Contas;
using PixApi.Application.Eventos;
using PixApi.Application.Portas;
using PixApi.Application.Transferencias;
using PixApi.Domain;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Application;

public class TransferenciaServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IContaRepositorio> _contas = new();
    private readonly Mock<ILancamentoRepositorio> _lancamentos = new();
    private readonly Mock<IPublicadorDeEventos> _publicador = new();
    private readonly List<string> _chamadas = [];
    private readonly UnidadeDeTrabalhoFalsa _uow;
    private readonly TransferenciaService _service;

    private readonly Conta _ana = Fabrica.Conta("ana@pix.com", 100m, 1);
    private readonly Conta _bruno = Fabrica.Conta("bruno@pix.com", 10m, 2);

    public TransferenciaServiceTests()
    {
        _uow = new UnidadeDeTrabalhoFalsa(_chamadas);
        _service = new TransferenciaService(_contas.Object, _lancamentos.Object, _uow, _publicador.Object,
            new FakeTimeProvider(Agora), NullLogger<TransferenciaService>.Instance);

        Existe(_ana);
        Existe(_bruno);
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<TransferenciaRealizadaEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => _chamadas.Add("publicar"))
            .Returns(Task.CompletedTask);
    }

    private void Existe(Conta conta) =>
        _contas.Setup(r => r.BuscarComLockPorChavePixAsync(conta.ChavePix, It.IsAny<CancellationToken>()))
            .Callback(() => _chamadas.Add("lock " + conta.ChavePix))
            .ReturnsAsync(conta);

    [Fact]
    public async Task Transferir_DeveDebitarCreditar_EDevolverComprovante()
    {
        var comprovante = await _service.TransferirAsync("ana@pix.com", "bruno@pix.com", 30m);

        Assert.Equal(70m, _ana.Saldo);
        Assert.Equal(40m, _bruno.Saldo);
        Assert.Equal(new ComprovanteTransferencia("ana@pix.com", "bruno@pix.com", 30m, 70m, Agora), comprovante);
    }

    [Fact]
    public async Task Transferir_DeveGravarPartidasDobradasNoRazao_ComOMesmoId()
    {
        var gravados = new List<Lancamento>();
        _lancamentos.Setup(r => r.Adicionar(It.IsAny<Lancamento>())).Callback<Lancamento>(gravados.Add);

        await _service.TransferirAsync("ana@pix.com", "bruno@pix.com", 30m);

        Assert.Collection(gravados,
            l => Assert.Equal((1L, TipoLancamento.PIX_ENVIADO, 30m), (l.ContaId, l.Tipo, l.Valor)),
            l => Assert.Equal((2L, TipoLancamento.PIX_RECEBIDO, 30m), (l.ContaId, l.Tipo, l.Valor)));
        Assert.NotNull(gravados[0].TransferenciaId);
        Assert.Equal(gravados[0].TransferenciaId, gravados[1].TransferenciaId);
    }

    [Fact]
    public async Task Transferir_DevePublicarOEvento_SoDepoisDoCommit()
    {
        TransferenciaRealizadaEvent? publicado = null;
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<TransferenciaRealizadaEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TransferenciaRealizadaEvent, CancellationToken>((e, _) => { publicado = e; _chamadas.Add("publicar"); })
            .Returns(Task.CompletedTask);

        await _service.TransferirAsync("ana@pix.com", "bruno@pix.com", 30m);

        Assert.Equal(["iniciar", "lock ana@pix.com", "lock bruno@pix.com", "salvar", "confirmar", "publicar"], _chamadas);
        Assert.Equal(("ana@pix.com", "bruno@pix.com", 30m, Agora),
            (publicado!.ChaveOrigem, publicado.ChaveDestino, publicado.Valor, publicado.RealizadaEm));
    }

    [Fact]
    public async Task Transferir_DeveTravarAsContasEmOrdemAlfabetica_QualquerQueSejaOSentido()
    {
        // bruno → ana trava "ana" primeiro: A→B e B→A simultâneas nunca esperam uma pela outra (deadlock).
        await _service.TransferirAsync("bruno@pix.com", "ana@pix.com", 5m);

        Assert.Equal(["lock ana@pix.com", "lock bruno@pix.com"], _chamadas.Where(c => c.StartsWith("lock")));
    }

    [Fact]
    public async Task Transferir_SemSaldo_NaoGravaNemPublica()
    {
        await Assert.ThrowsAsync<SaldoInsuficienteException>(() =>
            _service.TransferirAsync("bruno@pix.com", "ana@pix.com", 10.01m));

        Assert.DoesNotContain("salvar", _chamadas);
        Assert.DoesNotContain("confirmar", _chamadas);
        _lancamentos.Verify(r => r.Adicionar(It.IsAny<Lancamento>()), Times.Never);
        _publicador.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Transferir_ParaChaveInexistente_LancaChaveNaoEncontrada_ENaoPublica()
    {
        var erro = await Assert.ThrowsAsync<ChavePixNaoEncontradaException>(() =>
            _service.TransferirAsync("ana@pix.com", "ninguem@pix.com", 1m));

        Assert.Equal("Chave Pix não encontrada: ninguem@pix.com", erro.Message);
        Assert.DoesNotContain("confirmar", _chamadas);
        _publicador.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Transferir_ParaAPropriaChave_EhInvalida_ENemAbreTransacao()
    {
        await Assert.ThrowsAsync<TransferenciaInvalidaException>(() =>
            _service.TransferirAsync("ana@pix.com", "ana@pix.com", 1m));

        Assert.Empty(_chamadas);
    }

    [Fact]
    public async Task Transferir_QuandoAPublicacaoFalha_ATransferenciaContinuaConfirmada()
    {
        // O commit já aconteceu: avisar "deu erro" ao cliente faria ele tentar de novo e pagar duas vezes.
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<TransferenciaRealizadaEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker fora do ar"));

        var comprovante = await _service.TransferirAsync("ana@pix.com", "bruno@pix.com", 30m);

        Assert.Equal(70m, comprovante.SaldoOrigem);
        Assert.True(_uow.Confirmada);
    }
}
