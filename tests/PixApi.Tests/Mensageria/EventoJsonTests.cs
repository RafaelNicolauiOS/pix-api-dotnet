using System.Text.Json;
using PixApi.Application.Eventos;
using PixApi.Infrastructure.Mensageria;

namespace PixApi.Tests.Mensageria;

// O JSON da mensagem é um contrato com consumidores em qualquer linguagem: mesmo formato do pix-api.
public class EventoJsonTests
{
    private static readonly TransferenciaRealizadaEvent Evento = new(
        Guid.Parse("9ad998de-6f5f-472a-8552-3b963c83d626"), "ana@pix.com", "bruno@pix.com", 12.34m,
        new DateTimeOffset(2026, 10, 25, 16, 16, 25, 300, TimeSpan.Zero));

    [Fact]
    public void Serializa_IgualAoPixApiJava()
    {
        var json = JsonSerializer.Serialize(Evento, JsonPix.Opcoes);

        Assert.Equal("""{"id":"9ad998de-6f5f-472a-8552-3b963c83d626","chaveOrigem":"ana@pix.com","chaveDestino":"bruno@pix.com","valor":12.34,"realizadaEm":"2026-10-25T16:16:25.3Z"}""",
            json);
    }

    [Fact]
    public void LeDeVolta_OMesmoEvento_InclusiveComOffset()
    {
        Assert.Equal(Evento, JsonSerializer.Deserialize<TransferenciaRealizadaEvent>(
            JsonSerializer.Serialize(Evento, JsonPix.Opcoes), JsonPix.Opcoes));

        var comOffset = JsonSerializer.Deserialize<TransferenciaRealizadaEvent>(
            """{"id":"9ad998de-6f5f-472a-8552-3b963c83d626","chaveOrigem":"a","chaveDestino":"b","valor":1,"realizadaEm":"2026-10-25T13:16:25.3-03:00"}""",
            JsonPix.Opcoes)!;
        Assert.Equal(Evento.RealizadaEm, comOffset.RealizadaEm);
    }
}
