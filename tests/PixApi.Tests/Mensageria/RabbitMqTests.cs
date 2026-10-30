using Microsoft.Extensions.DependencyInjection;
using PixApi.Application.Eventos;
using PixApi.Application.Portas;
using PixApi.Infrastructure.Mensageria;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Mensageria;

public class RabbitMqFactory : PixApiFactory
{
    // Fila exclusiva desta execução: testes paralelos ou execuções antigas não "roubam" mensagens.
    public string Fila { get; } = $"pix.notificacoes.teste-{Guid.NewGuid():N}";

    protected override Dictionary<string, string?> Configuracoes => new()
    {
        ["Pix:Mensageria"] = "rabbitmq",
        ["Pix:RabbitMq:Url"] = Environment.GetEnvironmentVariable(RabbitMqFactAttribute.Variavel),
        ["Pix:RabbitMq:Fila"] = Fila,
        ["Pix:RabbitMq:FilaTemporaria"] = "true",
    };
}

// Equivale ao KafkaMensageriaIntegrationTest (@EmbeddedKafka) do Java, mas contra um RabbitMQ de verdade:
// não existe um RabbitMQ "embutido" para .NET. No CI o broker sobe como service do GitHub Actions.
public class RabbitMqTests(RabbitMqFactory api) : IClassFixture<RabbitMqFactory>
{
    private readonly HttpClient _http = api.CreateClient();

    private NotificacaoConsumer Consumidor => api.Services.GetRequiredService<NotificacaoConsumer>();

    [RabbitMqFact]
    public async Task Transferencia_ViraMensagemNaFila_EONotificadorAvisaQuemRecebeu()
    {
        var (a, b) = (HttpExtensions.ChaveUnica(), HttpExtensions.ChaveUnica());
        await _http.CriarConta(a, "100");
        await _http.CriarConta(b, "0");

        await _http.PostJson("/transferencias", $$"""{"chaveOrigem": "{{a}}", "chaveDestino": "{{b}}", "valor": 12.34}""");

        await Esperar.Ate(() => Consumidor.Notificacoes().Any(n => n.Contains(b)));
        Assert.Contains($"Notificação para {b}: você recebeu um Pix de R$ 12,34 de {a}", Consumidor.Notificacoes());
    }

    [RabbitMqFact]
    public async Task MensagemDuplicada_NoBroker_EhNotificadaUmaVezSo()
    {
        var publicador = api.Services.GetRequiredKeyedService<IPublicadorDeEventos>(PublicadorComHistorico.ChaveDoAdaptador);
        Assert.IsType<RabbitMqPublicadorDeEventos>(publicador);
        var destino = HttpExtensions.ChaveUnica();
        var evento = new TransferenciaRealizadaEvent(Guid.NewGuid(), "x@pix.com", destino, 1m, DateTimeOffset.UtcNow);

        await publicador.PublicarAsync(evento);
        await publicador.PublicarAsync(evento); // reentrega
        await publicador.PublicarAsync(evento with { Id = Guid.NewGuid(), Valor = 2m }); // marcador: chegou depois

        await Esperar.Ate(() => Consumidor.Notificacoes().Any(n => n.Contains(destino) && n.Contains("R$ 2,00")));
        Assert.Single(Consumidor.Notificacoes(), n => n.Contains(destino) && n.Contains("R$ 1,00"));
    }
}
