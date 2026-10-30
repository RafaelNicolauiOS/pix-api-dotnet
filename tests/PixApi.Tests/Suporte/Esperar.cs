namespace PixApi.Tests.Suporte;

// Espera algo assíncrono acontecer (o Awaitility do Java): tenta até dar certo ou estourar o prazo.
public static class Esperar
{
    public static async Task Ate(Func<bool> condicao, TimeSpan? prazo = null)
    {
        var limite = DateTime.UtcNow + (prazo ?? TimeSpan.FromSeconds(10));
        while (!condicao())
        {
            if (DateTime.UtcNow > limite)
            {
                throw new TimeoutException("A condição não foi atendida dentro do prazo");
            }
            await Task.Delay(50);
        }
    }
}
