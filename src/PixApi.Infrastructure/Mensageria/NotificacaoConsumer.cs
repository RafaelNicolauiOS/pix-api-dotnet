using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PixApi.Application.Eventos;

namespace PixApi.Infrastructure.Mensageria;

// Consumidor de exemplo: simula um serviço de notificações que avisa quem recebeu o Pix.
// Num sistema real seria outra aplicação; fica aqui para fins didáticos (igual ao Java).
// A lógica não depende do RabbitMQ (fica testável sem broker); RabbitMqConsumidor só entrega as mensagens.
public sealed class NotificacaoConsumer(ILogger<NotificacaoConsumer> log)
{
    private const int MaxNotificacoes = 50;
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    // O broker entrega "pelo menos uma vez": a mesma mensagem pode chegar repetida (ex.: o consumidor
    // caiu antes do ack). Guardamos os ids já processados para não notificar duas vezes (idempotência).
    // Em produção esse controle ficaria no banco, na mesma transação do efeito (tabela com o id UNIQUE).
    private readonly ConcurrentDictionary<Guid, byte> _processados = new();
    private readonly LinkedList<string> _notificacoes = new();

    // Devolve false se a mensagem for uma duplicata.
    public bool Processar(string mensagem)
    {
        var evento = JsonSerializer.Deserialize<TransferenciaRealizadaEvent>(mensagem, JsonPix.Opcoes)
                     ?? throw new JsonException("mensagem vazia");
        if (!_processados.TryAdd(evento.Id, 0))
        {
            log.LogInformation("Evento {Id} já processado, ignorando duplicata", evento.Id);
            return false;
        }

        var notificacao = $"Notificação para {evento.ChaveDestino}: você recebeu um Pix de " +
                          $"{evento.Valor.ToString("C2", PtBr).Replace(' ', ' ')} de {evento.ChaveOrigem}";
        log.LogInformation("{Notificacao}", notificacao);

        lock (_notificacoes)
        {
            _notificacoes.AddFirst(notificacao);
            while (_notificacoes.Count > MaxNotificacoes)
            {
                _notificacoes.RemoveLast();
            }
        }
        return true;
    }

    // Últimas notificações, da mais recente para a mais antiga.
    public IReadOnlyList<string> Notificacoes()
    {
        lock (_notificacoes)
        {
            return _notificacoes.ToList();
        }
    }
}
