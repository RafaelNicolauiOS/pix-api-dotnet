namespace PixApi.Tests.Suporte;

// Testes que precisam de um serviço externo de verdade (PostgreSQL, RabbitMQ). Sem a variável de
// ambiente, o xUnit os mostra como "Ignorado" com o motivo, em vez de falhar. No CI elas são definidas
// (o workflow sobe os dois como "services"), então lá esses testes rodam sempre.
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string Variavel = "PIX_TESTE_POSTGRES";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variavel)))
        {
            Skip = $"Defina {Variavel} (ex.: Host=localhost;Username=pix;Password=pix) para rodar contra o PostgreSQL.";
        }
    }
}

public sealed class RabbitMqFactAttribute : FactAttribute
{
    public const string Variavel = "PIX_TESTE_RABBITMQ";

    public RabbitMqFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variavel)))
        {
            Skip = $"Defina {Variavel} (ex.: amqp://guest:guest@localhost:5672) para rodar contra o RabbitMQ.";
        }
    }
}
