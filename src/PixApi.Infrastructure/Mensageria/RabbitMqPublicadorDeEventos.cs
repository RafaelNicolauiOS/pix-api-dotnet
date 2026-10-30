using System.Text.Json;
using Microsoft.Extensions.Logging;
using PixApi.Application.Eventos;
using PixApi.Application.Portas;
using RabbitMQ.Client;

namespace PixApi.Infrastructure.Mensageria;

// Adaptador RabbitMQ (Pix:Mensageria = rabbitmq). Equivale ao KafkaPublicadorDeEventos do Java.
public sealed class RabbitMqPublicadorDeEventos(RabbitMqConexao conexao, ILogger<RabbitMqPublicadorDeEventos> log)
    : IPublicadorDeEventos, IDisposable
{
    private readonly object _trava = new();
    private IModel? _canal;

    public Task PublicarAsync(TransferenciaRealizadaEvent evento, CancellationToken ct = default)
    {
        // JSON gerado por nós, sem nomes de classes .NET: legível por consumidores em qualquer linguagem.
        var corpo = JsonSerializer.SerializeToUtf8Bytes(evento, JsonPix.Opcoes);
        var opcoes = conexao.Opcoes;

        // Um canal só, protegido por lock (canal não é thread-safe). Simples e suficiente para o volume
        // deste projeto; com muita carga, um pool de canais evitaria a fila no lock.
        lock (_trava)
        {
            try
            {
                if (_canal is null or { IsOpen: false })
                {
                    _canal = conexao.CriarCanal();
                    _canal.ConfirmSelect(); // "publisher confirms": o broker avisa que recebeu e gravou
                }
                var propriedades = _canal.CreateBasicProperties();
                propriedades.Persistent = true;                // sobrevive a um restart do broker
                propriedades.MessageId = evento.Id.ToString(); // o consumidor deduplica por ele
                propriedades.ContentType = "application/json";
                propriedades.Type = "TransferenciaRealizada";

                _canal.BasicPublish(opcoes.Exchange, opcoes.RoutingKey, mandatory: false, propriedades, corpo);
                _canal.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5)); // sem confirmação → exceção
            }
            catch
            {
                _canal?.Dispose();
                _canal = null; // recria na próxima tentativa
                throw;
            }
        }
        log.LogInformation("Evento {Id} publicado em {Exchange} ({RoutingKey})", evento.Id, opcoes.Exchange, opcoes.RoutingKey);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_trava)
        {
            _canal?.Dispose();
        }
    }
}
