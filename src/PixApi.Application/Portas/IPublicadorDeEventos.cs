using PixApi.Application.Eventos;

namespace PixApi.Application.Portas;

// O caso de uso diz O QUE precisa (publicar um evento), sem saber COMO.
// É o papel de um protocol em Swift: os adaptadores (log, RabbitMQ) ficam na Infrastructure e são
// escolhidos por configuração (Pix:Mensageria), sem mexer na regra de negócio.
public interface IPublicadorDeEventos
{
    Task PublicarAsync(TransferenciaRealizadaEvent evento, CancellationToken ct = default);
}
