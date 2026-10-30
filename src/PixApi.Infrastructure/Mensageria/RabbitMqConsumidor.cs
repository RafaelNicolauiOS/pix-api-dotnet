using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PixApi.Infrastructure.Mensageria;

// BackgroundService = tarefa de longa duração que sobe e desce junto com a aplicação
// (o @KafkaListener do Spring faz isso por baixo dos panos). Só liga com Pix:Mensageria = rabbitmq.
public sealed class RabbitMqConsumidor(RabbitMqConexao conexao, NotificacaoConsumer notificacoes, IConfiguration config,
                                       ILogger<RabbitMqConsumidor> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken parar)
    {
        if (!DependencyInjection.UsaRabbitMq(config))
        {
            return;
        }
        // Se o broker estiver fora do ar na subida, tenta de novo em vez de derrubar a API.
        while (!parar.IsCancellationRequested)
        {
            try
            {
                using var canal = conexao.CriarCanal();
                canal.BasicQos(prefetchSize: 0, prefetchCount: 20, global: false); // no máximo 20 sem ack
                var consumidor = new AsyncEventingBasicConsumer(canal);
                consumidor.Received += (_, entrega) => Receber(canal, entrega);
                // autoAck: false → o ack é manual, DEPOIS de processar. Se a API cair no meio,
                // o broker reentrega a mensagem (por isso o consumidor precisa ser idempotente).
                canal.BasicConsume(conexao.Opcoes.Fila, autoAck: false, consumidor);
                log.LogInformation("Consumindo a fila {Fila}", conexao.Opcoes.Fila);

                while (canal.IsOpen && !parar.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), parar);
                }
            }
            catch (OperationCanceledException) when (parar.IsCancellationRequested)
            {
                return;
            }
            catch (Exception erro)
            {
                log.LogWarning(erro, "RabbitMQ indisponível; nova tentativa em 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), parar).ContinueWith(_ => { });
            }
        }
    }

    private Task Receber(IModel canal, BasicDeliverEventArgs entrega)
    {
        try
        {
            notificacoes.Processar(Encoding.UTF8.GetString(entrega.Body.Span));
            canal.BasicAck(entrega.DeliveryTag, multiple: false);
        }
        catch (JsonException erro)
        {
            // Mensagem que nunca vai ser lida ("poison message"): descarta em vez de reentregar para sempre.
            // Em produção iria para uma dead-letter queue.
            log.LogError(erro, "Mensagem inválida descartada");
            canal.BasicNack(entrega.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception erro)
        {
            log.LogError(erro, "Falha ao processar mensagem; devolvendo à fila");
            canal.BasicNack(entrega.DeliveryTag, multiple: false, requeue: true);
        }
        return Task.CompletedTask;
    }
}
