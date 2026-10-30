using PixApi.Application.Eventos;

namespace PixApi.Application.Monitoramento;

public class HistoricoDeEventos(int capacidade)
{
    public void Registrar(TransferenciaRealizadaEvent evento) => throw new NotImplementedException();
    public IReadOnlyList<TransferenciaRealizadaEvent> Recentes() => throw new NotImplementedException();
}
