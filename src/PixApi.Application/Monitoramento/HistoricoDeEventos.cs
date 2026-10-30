using PixApi.Application.Eventos;

namespace PixApi.Application.Monitoramento;

// Guarda os últimos eventos em memória (buffer circular: ao encher, descarta o mais antigo).
// Só para a página Atividade (GET /eventos): zera quando a aplicação reinicia.
public class HistoricoDeEventos(int capacidade)
{
    private readonly LinkedList<TransferenciaRealizadaEvent> _eventos = new();
    private readonly object _trava = new();

    // lock: várias requisições (threads) registram ao mesmo tempo. É o "synchronized" do Java;
    // em Swift seria proteger o array com um actor.
    public void Registrar(TransferenciaRealizadaEvent evento)
    {
        lock (_trava)
        {
            _eventos.AddFirst(evento);
            while (_eventos.Count > capacidade)
            {
                _eventos.RemoveLast();
            }
        }
    }

    public IReadOnlyList<TransferenciaRealizadaEvent> Recentes()
    {
        lock (_trava)
        {
            return _eventos.ToList();
        }
    }
}
