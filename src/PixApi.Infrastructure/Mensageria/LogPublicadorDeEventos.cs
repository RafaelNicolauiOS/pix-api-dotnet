using Microsoft.Extensions.Logging;
using PixApi.Application.Eventos;
using PixApi.Application.Portas;

namespace PixApi.Infrastructure.Mensageria;

// Adaptador padrão (Pix:Mensageria = log): só registra o evento no log. Nenhum broker é necessário.
public sealed class LogPublicadorDeEventos(ILogger<LogPublicadorDeEventos> log) : IPublicadorDeEventos
{
    public Task PublicarAsync(TransferenciaRealizadaEvent evento, CancellationToken ct = default)
    {
        log.LogInformation("Evento TransferenciaRealizada id={Id} {Origem} -> {Destino} valor={Valor} em={Em:O}",
            evento.Id, evento.ChaveOrigem, evento.ChaveDestino, evento.Valor, evento.RealizadaEm);
        return Task.CompletedTask;
    }
}
