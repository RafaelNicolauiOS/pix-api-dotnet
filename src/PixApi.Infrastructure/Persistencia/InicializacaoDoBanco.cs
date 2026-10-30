using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PixApi.Infrastructure.Persistencia;

// Roda na subida, antes de a API aceitar requisições (o Flyway do Spring Boot faz o mesmo).
//  - PostgreSQL: aplica as migrations pendentes (tabela __EFMigrationsHistory = flyway_schema_history).
//  - SQLite: cria o schema a partir do modelo (as migrations são SQL de PostgreSQL).
public class InicializacaoDoBanco(IServiceProvider servicos, ILogger<InicializacaoDoBanco> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var escopo = servicos.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<PixDbContext>();
        if (db.Database.IsNpgsql())
        {
            var pendentes = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            await db.Database.MigrateAsync(ct);
            log.LogInformation("Migrations aplicadas: {Quantidade} ({Nomes})", pendentes.Count, string.Join(", ", pendentes));
        }
        else
        {
            await db.Database.EnsureCreatedAsync(ct);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
