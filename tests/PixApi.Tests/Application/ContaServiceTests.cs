using Microsoft.Extensions.Time.Testing;
using Moq;
using PixApi.Application.Contas;
using PixApi.Application.Portas;
using PixApi.Domain;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Application;

// Unidade com mocks (Moq = Mockito do .NET): o repositório é falso, a regra é real.
public class ContaServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IContaRepositorio> _contas = new();
    private readonly Mock<ILancamentoRepositorio> _lancamentos = new();
    private readonly List<string> _chamadas = [];
    private readonly UnidadeDeTrabalhoFalsa _uow;
    private readonly ContaService _service;

    public ContaServiceTests()
    {
        _uow = new UnidadeDeTrabalhoFalsa(_chamadas);
        _service = new ContaService(_contas.Object, _lancamentos.Object, _uow, new FakeTimeProvider(Agora));
    }

    [Fact]
    public async Task Criar_DeveGravarContaEAberturaNoRazao_NaMesmaTransacao()
    {
        var conta = Fabrica.Conta("ana@pix.com", 100m);
        _uow.AoSalvar = () => conta.ComId(7); // o banco gera o id no primeiro SaveChanges

        var criada = await _service.CriarAsync(conta);

        Assert.Same(conta, criada);
        _contas.Verify(r => r.Adicionar(conta));
        _lancamentos.Verify(r => r.Adicionar(It.Is<Lancamento>(l =>
            l.ContaId == 7 && l.Tipo == TipoLancamento.ABERTURA && l.Valor == 100m && l.RealizadoEm == Agora)));
        Assert.True(_uow.Confirmada);
    }

    [Fact]
    public async Task Criar_ComChaveDuplicada_DeveLancarExcecao_ENaoGravar()
    {
        _contas.Setup(r => r.ExisteChavePixAsync("ana@pix.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ChavePixDuplicadaException>(() => _service.CriarAsync(Fabrica.Conta("ana@pix.com", 1m)));

        _contas.Verify(r => r.Adicionar(It.IsAny<Conta>()), Times.Never);
        Assert.False(_uow.Confirmada);
    }

    [Fact]
    public async Task Criar_QuandoOutraRequisicaoGravaAMesmaChaveAntes_DeveLancarChaveDuplicada()
    {
        // Corrida: as duas requisições passaram pelo ExisteChavePix; o UNIQUE do banco barra a segunda.
        _uow.ErroAoSalvar = new ConflitoDeChaveUnicaException(new Exception("23505"));

        await Assert.ThrowsAsync<ChavePixDuplicadaException>(() => _service.CriarAsync(Fabrica.Conta("ana@pix.com", 1m)));
    }

    [Fact]
    public async Task BuscarPorId_Inexistente_DeveLancarContaNaoEncontrada()
    {
        var erro = await Assert.ThrowsAsync<ContaNaoEncontradaException>(() => _service.BuscarPorIdAsync(99));

        Assert.Equal("Conta não encontrada: id=99", erro.Message);
    }

    [Fact]
    public async Task Listar_DeveDevolverAsContasOrdenadasDoRepositorio()
    {
        IReadOnlyList<Conta> todas = [Fabrica.Conta("a@pix.com", 1m, 1), Fabrica.Conta("b@pix.com", 2m, 2)];
        _contas.Setup(r => r.ListarOrdenadoPorIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(todas);

        Assert.Equal(todas, await _service.ListarAsync());
    }

    [Fact]
    public async Task Atualizar_DeveTrocarTitularEChave()
    {
        var conta = Fabrica.Conta("ana@pix.com", 10m, 1);
        _contas.Setup(r => r.BuscarPorIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        var atualizada = await _service.AtualizarAsync(1, "Ana Lima", "ana.lima@pix.com");

        Assert.Equal("Ana Lima", atualizada.Titular);
        Assert.Equal("ana.lima@pix.com", atualizada.ChavePix);
        Assert.Contains("salvar", _chamadas);
    }

    [Fact]
    public async Task Atualizar_ParaChaveDeOutraConta_DeveLancarChaveDuplicada()
    {
        _contas.Setup(r => r.BuscarPorIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Fabrica.Conta("ana@pix.com", 10m, 1));
        _contas.Setup(r => r.ExisteChavePixAsync("bruno@pix.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ChavePixDuplicadaException>(() => _service.AtualizarAsync(1, "Ana", "bruno@pix.com"));
        Assert.DoesNotContain("salvar", _chamadas);
    }

    [Fact]
    public async Task Atualizar_MantendoAPropriaChave_NaoAcusaDuplicidade()
    {
        _contas.Setup(r => r.BuscarPorIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Fabrica.Conta("ana@pix.com", 10m, 1));
        _contas.Setup(r => r.ExisteChavePixAsync("ana@pix.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var atualizada = await _service.AtualizarAsync(1, "Ana Nova", "ana@pix.com");

        Assert.Equal("Ana Nova", atualizada.Titular);
    }

    [Fact]
    public async Task Remover_ComSaldo_DeveLancarEncerramento_EExcluir()
    {
        var conta = Fabrica.Conta("ana@pix.com", 70m, 3);
        _contas.Setup(r => r.BuscarPorIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        await _service.RemoverAsync(3);

        _lancamentos.Verify(r => r.Adicionar(It.Is<Lancamento>(l =>
            l.ContaId == 3 && l.Tipo == TipoLancamento.ENCERRAMENTO && l.Valor == 70m)));
        _contas.Verify(r => r.Remover(conta));
        Assert.True(_uow.Confirmada);
    }

    [Fact]
    public async Task Remover_SemSaldo_NaoLancaNadaNoRazao()
    {
        _contas.Setup(r => r.BuscarPorIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(Fabrica.Conta("a@pix.com", 0m, 3));

        await _service.RemoverAsync(3);

        _lancamentos.Verify(r => r.Adicionar(It.IsAny<Lancamento>()), Times.Never);
    }

    [Fact]
    public async Task Remover_Inexistente_DeveLancarContaNaoEncontrada()
    {
        await Assert.ThrowsAsync<ContaNaoEncontradaException>(() => _service.RemoverAsync(99));
        _contas.Verify(r => r.Remover(It.IsAny<Conta>()), Times.Never);
    }
}
