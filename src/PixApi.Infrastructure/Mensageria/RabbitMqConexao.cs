using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace PixApi.Infrastructure.Mensageria;

// Uma conexão TCP por aplicação (cara de abrir) e vários canais leves sobre ela.
// Conexão: thread-safe. Canal (IModel): NÃO é thread-safe, cada usuário tem o seu.
public sealed class RabbitMqConexao(IOptions<RabbitMqOpcoes> opcoes) : IDisposable
{
    private readonly object _trava = new();
    private IConnection? _conexao;

    public RabbitMqOpcoes Opcoes => opcoes.Value;

    // Cria um canal e garante que exchange, fila e ligação existem (declarar é idempotente).
    public IModel CriarCanal()
    {
        var canal = Conexao().CreateModel();
        canal.ExchangeDeclare(Opcoes.Exchange, ExchangeType.Topic, durable: true);
        // Filas sempre duráveis (o RabbitMQ 4 não aceita fila transitória compartilhada). A temporária dos
        // testes expira sozinha após 1 min sem uso (x-expires).
        var argumentos = Opcoes.FilaTemporaria ? new Dictionary<string, object> { ["x-expires"] = 60_000 } : null;
        canal.QueueDeclare(Opcoes.Fila, durable: true, exclusive: false, autoDelete: false, argumentos);
        canal.QueueBind(Opcoes.Fila, Opcoes.Exchange, Opcoes.RoutingKey);
        return canal;
    }

    private IConnection Conexao()
    {
        lock (_trava)
        {
            if (_conexao is { IsOpen: true })
            {
                return _conexao;
            }
            _conexao?.Dispose();
            var fabrica = new ConnectionFactory
            {
                Uri = new Uri(Opcoes.Url),
                ClientProvidedName = "pix-api-dotnet",
                DispatchConsumersAsync = true,       // handlers async no consumidor
                AutomaticRecoveryEnabled = true,     // reconecta sozinho se o broker reiniciar
            };
            return _conexao = fabrica.CreateConnection();
        }
    }

    public void Dispose()
    {
        lock (_trava)
        {
            _conexao?.Dispose();
        }
    }
}
