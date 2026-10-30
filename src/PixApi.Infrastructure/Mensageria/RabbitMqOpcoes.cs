namespace PixApi.Infrastructure.Mensageria;

// Seção "Pix:RabbitMq" do appsettings.json (ou variáveis Pix__RabbitMq__Url etc.).
public sealed class RabbitMqOpcoes
{
    public string Url { get; set; } = "amqp://guest:guest@localhost:5672";

    // Exchange "topic": o publicador não conhece filas, só a exchange e a routing key.
    // Cada sistema interessado (notificações, antifraude...) liga a própria fila nela.
    public string Exchange { get; set; } = "pix.transferencias";
    public string RoutingKey { get; set; } = "transferencia.realizada";
    public string Fila { get; set; } = "pix.notificacoes";

    // Só para testes: a fila expira sozinha depois de 1 minuto sem uso.
    public bool FilaTemporaria { get; set; }
}
