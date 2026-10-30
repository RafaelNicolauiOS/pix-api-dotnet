using PixApi.Application.Eventos;
using PixApi.Application.Monitoramento;

namespace PixApi.Tests.Application;

public class HistoricoDeEventosTests
{
    private static TransferenciaRealizadaEvent Evento(decimal valor) =>
        new(Guid.NewGuid(), "a@pix.com", "b@pix.com", valor, DateTimeOffset.UtcNow);

    [Fact]
    public void Recentes_DevolveOMaisRecentePrimeiro()
    {
        var historico = new HistoricoDeEventos(10);
        historico.Registrar(Evento(1));
        historico.Registrar(Evento(2));

        Assert.Equal([2m, 1m], historico.Recentes().Select(e => e.Valor));
    }

    [Fact]
    public void AoEncher_DescartaOMaisAntigo()
    {
        var historico = new HistoricoDeEventos(2);
        historico.Registrar(Evento(1));
        historico.Registrar(Evento(2));
        historico.Registrar(Evento(3));

        Assert.Equal([3m, 2m], historico.Recentes().Select(e => e.Valor));
    }
}
