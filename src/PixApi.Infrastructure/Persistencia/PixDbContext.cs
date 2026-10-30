using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PixApi.Domain;

namespace PixApi.Infrastructure.Persistencia;

// DbContext = "sessão" do EF Core com o banco (o EntityManager/Session do JPA/Hibernate).
// Vive um por requisição (Scoped): rastreia as entidades lidas e, no SaveChanges, gera os
// INSERT/UPDATE/DELETE do que mudou (o "dirty checking" do Hibernate se chama ChangeTracker aqui).
public class PixDbContext(DbContextOptions<PixDbContext> options) : DbContext(options)
{
    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PixDbContext).Assembly);

        // SQLite (testes e desenvolvimento) não tem tipo de data com fuso: o EF não consegue fazer
        // ORDER BY nem "<=" em DateTimeOffset. Gravamos como número (ticks UTC), que ordena certo.
        // No PostgreSQL a coluna é "timestamp with time zone", como no projeto Java.
        if (Database.IsSqlite())
        {
            var propriedades = modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()).ToList();
            foreach (var propriedade in propriedades.Where(p => p.ClrType == typeof(DateTimeOffset)))
            {
                propriedade.SetValueConverter(new DateTimeOffsetToBinaryConverter());
            }
            // O SQLite guarda decimal como texto e devolve "1000.0"; o NUMERIC(15,2) do PostgreSQL devolve
            // "1000.00". Somar 0.00m dá ao decimal a escala 2 (o decimal guarda a escala), e o JSON sai igual.
            foreach (var propriedade in propriedades.Where(p => p.ClrType == typeof(decimal)))
            {
                propriedade.SetValueConverter(new ValueConverter<decimal, decimal>(v => v, v => v + 0.00m));
            }
        }
    }
}
