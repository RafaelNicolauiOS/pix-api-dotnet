using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PixApi.Infrastructure.Persistencia;

// Usada só pelo "dotnet ef migrations add": as migrations são sempre geradas para o PostgreSQL,
// independentemente da configuração local. A conexão não é aberta para gerar o código.
public class PixDbContextDesignFactory : IDesignTimeDbContextFactory<PixDbContext>
{
    public PixDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PixDbContext>().UseNpgsql("Host=localhost;Database=pix").Options);
}
