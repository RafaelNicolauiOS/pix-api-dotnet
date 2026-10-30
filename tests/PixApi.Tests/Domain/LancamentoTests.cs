using PixApi.Domain;

namespace PixApi.Tests.Domain;

public class LancamentoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creditos_TemValorPositivo_EDebitosNegativo()
    {
        var id = Guid.NewGuid();

        Assert.Equal(100m, Lancamento.Abertura(1, 100m, Agora).ValorComSinal());
        Assert.Equal(30m, Lancamento.PixRecebido(1, 30m, Agora, id).ValorComSinal());
        Assert.Equal(-30m, Lancamento.PixEnviado(1, 30m, Agora, id).ValorComSinal());
        Assert.Equal(-70m, Lancamento.Encerramento(1, 70m, Agora).ValorComSinal());
    }

    [Fact]
    public void PixEnviado_GuardaContaTipoDataEIdDaTransferencia()
    {
        var id = Guid.NewGuid();

        var l = Lancamento.PixEnviado(7, 12.34m, Agora, id);

        Assert.Equal(7, l.ContaId);
        Assert.Equal(TipoLancamento.PIX_ENVIADO, l.Tipo);
        Assert.Equal(12.34m, l.Valor);
        Assert.Equal(Agora, l.RealizadoEm);
        Assert.Equal(id, l.TransferenciaId);
    }

    [Fact]
    public void Abertura_NaoTemTransferencia()
    {
        Assert.Null(Lancamento.Abertura(1, 10m, Agora).TransferenciaId);
    }
}
