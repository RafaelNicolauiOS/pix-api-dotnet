using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PixApi.Application.Monitoramento;
using PixApi.Application.Portas;
using PixApi.Infrastructure.Mensageria;
using PixApi.Infrastructure.Persistencia;

namespace PixApi.Infrastructure;

// Registra os adaptadores da Infrastructure no container de DI (o que o component scan do Spring
// fazia sozinho com @Repository/@Component). Tempos de vida:
//  - Scoped: um por requisição HTTP (DbContext, repositórios, unidade de trabalho);
//  - Singleton: um para a aplicação inteira (conexão do RabbitMQ, histórico em memória).
public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection servicos)
    {
        // A configuração é lida quando o DbContext é criado (e não aqui), para os testes poderem trocá-la.
        servicos.AddDbContext<PixDbContext>((sp, opcoes) => ConfigurarBanco(opcoes, sp.GetRequiredService<IConfiguration>()));
        servicos.AddScoped<IContaRepositorio, ContaRepositorio>();
        servicos.AddScoped<ILancamentoRepositorio, LancamentoRepositorio>();
        servicos.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalhoEf>();
        servicos.AddHostedService<InicializacaoDoBanco>();

        servicos.AddSingleton(new HistoricoDeEventos(100));
        servicos.AddOptions<RabbitMqOpcoes>().BindConfiguration("Pix:RabbitMq");
        servicos.AddSingleton<RabbitMqConexao>();
        servicos.AddSingleton<NotificacaoConsumer>();
        servicos.AddHostedService<RabbitMqConsumidor>();

        // Porta e adaptadores: o adaptador é escolhido por configuração (Pix:Mensageria = log | rabbitmq),
        // como o @ConditionalOnProperty do Java. No modo log nada do RabbitMQ é criado nem conectado.
        servicos.AddKeyedSingleton<IPublicadorDeEventos>(PublicadorComHistorico.ChaveDoAdaptador, (sp, _) =>
            UsaRabbitMq(sp.GetRequiredService<IConfiguration>())
                ? ActivatorUtilities.CreateInstance<RabbitMqPublicadorDeEventos>(sp)
                : ActivatorUtilities.CreateInstance<LogPublicadorDeEventos>(sp));
        servicos.AddSingleton<IPublicadorDeEventos>(sp => new PublicadorComHistorico(
            sp.GetRequiredService<HistoricoDeEventos>(),
            sp.GetRequiredKeyedService<IPublicadorDeEventos>(PublicadorComHistorico.ChaveDoAdaptador)));
        return servicos;
    }

    public static bool UsaRabbitMq(IConfiguration config) => (config["Pix:Mensageria"] ?? "log").ToLowerInvariant() switch
    {
        "log" => false,
        "rabbitmq" => true,
        var outro => throw new InvalidOperationException($"Pix:Mensageria inválido: '{outro}'. Use 'log' ou 'rabbitmq'."),
    };

    private static void ConfigurarBanco(DbContextOptionsBuilder opcoes, IConfiguration config)
    {
        var conexao = StringDeConexao(config);
        switch ((config["Pix:Banco:Provedor"] ?? "postgres").ToLowerInvariant())
        {
            case "postgres":
                opcoes.UseNpgsql(conexao);
                break;
            case "sqlite":
                opcoes.UseSqlite(conexao);
                break;
            case var outro:
                throw new InvalidOperationException($"Pix:Banco:Provedor inválido: '{outro}'. Use 'postgres' ou 'sqlite'.");
        }
    }

    // ConnectionStrings:Pix, ou as variáveis DB_HOST/DB_PORT/DB_NAME/DB_USER/DB_PASSWORD que o Render
    // injeta (as mesmas do pix-api Java), para o render.yaml funcionar igual.
    private static string StringDeConexao(IConfiguration config)
    {
        var host = config["DB_HOST"];
        if (!string.IsNullOrWhiteSpace(host))
        {
            return new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = int.TryParse(config["DB_PORT"], out var porta) ? porta : 5432,
                Database = config["DB_NAME"],
                Username = config["DB_USER"],
                Password = config["DB_PASSWORD"],
                MaxPoolSize = 5, // o PostgreSQL gratuito do Render aceita poucas conexões
            }.ConnectionString;
        }
        return config.GetConnectionString("Pix")
               ?? throw new InvalidOperationException("Defina ConnectionStrings:Pix (ou DB_HOST etc.).");
    }
}
