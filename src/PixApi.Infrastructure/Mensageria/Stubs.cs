using System.Text.Json;
using Microsoft.Extensions.Logging;
using PixApi.Application.Eventos;
using PixApi.Application.Portas;

namespace PixApi.Infrastructure.Mensageria;

public static class JsonPix { public static readonly JsonSerializerOptions Opcoes = new(); }

public class PublicadorComHistorico { public const string ChaveDoAdaptador = "adaptador"; }

public class RabbitMqPublicadorDeEventos : IPublicadorDeEventos
{
    public Task PublicarAsync(TransferenciaRealizadaEvent evento, CancellationToken ct = default) => throw new NotImplementedException();
}

public class NotificacaoConsumer(ILogger<NotificacaoConsumer> log)
{
    public bool Processar(string mensagem) => throw new NotImplementedException();
    public IReadOnlyList<string> Notificacoes() => throw new NotImplementedException();
}
