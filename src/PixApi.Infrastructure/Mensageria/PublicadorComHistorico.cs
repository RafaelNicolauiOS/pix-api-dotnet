using PixApi.Application.Eventos;
using PixApi.Application.Monitoramento;
using PixApi.Application.Portas;

namespace PixApi.Infrastructure.Mensageria;

// Decorator: é o IPublicadorDeEventos que o caso de uso recebe. Registra o evento no histórico
// (GET /eventos) e repassa ao adaptador configurado (log ou RabbitMQ).
// No Java eram dois @TransactionalEventListener independentes; aqui é um objeto que "embrulha" o outro.
public sealed class PublicadorComHistorico(HistoricoDeEventos historico, IPublicadorDeEventos adaptador) : IPublicadorDeEventos
{
    // Chave do adaptador no container de DI ("keyed service" do .NET 8).
    public const string ChaveDoAdaptador = "adaptador";

    public Task PublicarAsync(TransferenciaRealizadaEvent evento, CancellationToken ct = default)
    {
        historico.Registrar(evento); // primeiro: o feed funciona mesmo com o broker fora do ar
        return adaptador.PublicarAsync(evento, ct);
    }
}
