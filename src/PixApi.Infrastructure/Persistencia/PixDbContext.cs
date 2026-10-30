using Microsoft.EntityFrameworkCore;
using PixApi.Domain;

namespace PixApi.Infrastructure.Persistencia;

public class PixDbContext(DbContextOptions<PixDbContext> options) : DbContext(options)
{
    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
}
