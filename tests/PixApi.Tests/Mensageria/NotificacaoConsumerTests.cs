using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PixApi.Application.Eventos;
using PixApi.Infrastructure.Mensageria;

namespace PixApi.Tests.Mensageria;

public class NotificacaoConsumerTests
{
    private readonly NotificacaoConsumer _consumidor = new(NullLogger<NotificacaoConsumer>.Instance);

    private static string Mensagem(Guid id, decimal valor) => JsonSerializer.Serialize(
        new TransferenciaRealizadaEvent(id, "ana@pix.com", "bruno@pix.com", valor, DateTimeOffset.UtcNow), JsonPix.Opcoes);

    [Fact]
    public void Processar_GeraANotificacaoEmReais()
    {
        var processou = _consumidor.Processar(Mensagem(Guid.NewGuid(), 1234.5m));

        Assert.True(processou);
        Assert.Equal(["Notificação para bruno@pix.com: você recebeu um Pix de R$ 1.234,50 de ana@pix.com"],
            _consumidor.Notificacoes());
    }

    [Fact]
    public void MensagemRepetida_EhIgnorada_PeloId()
    {
        // "Pelo menos uma vez": o broker pode reentregar a mesma mensagem.
        var id = Guid.NewGuid();

        Assert.True(_consumidor.Processar(Mensagem(id, 10m)));
        Assert.False(_consumidor.Processar(Mensagem(id, 10m)));

        Assert.Single(_consumidor.Notificacoes());
    }

    [Fact]
    public void Notificacoes_MaisRecentePrimeiro()
    {
        _consumidor.Processar(Mensagem(Guid.NewGuid(), 1m));
        _consumidor.Processar(Mensagem(Guid.NewGuid(), 2m));

        Assert.StartsWith("Notificação para bruno@pix.com: você recebeu um Pix de R$ 2,00", _consumidor.Notificacoes()[0]);
    }
}
