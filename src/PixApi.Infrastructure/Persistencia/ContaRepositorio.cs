using Microsoft.EntityFrameworkCore;
using PixApi.Application.Portas;
using PixApi.Domain;

namespace PixApi.Infrastructure.Persistencia;

public class ContaRepositorio(PixDbContext db) : IContaRepositorio
{
    public Task<Conta?> BuscarPorIdAsync(long id, CancellationToken ct = default) =>
        db.Contas.SingleOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Conta>> ListarOrdenadoPorIdAsync(CancellationToken ct = default) =>
        await db.Contas.OrderBy(c => c.Id).ToListAsync(ct);

    public Task<bool> ExisteChavePixAsync(string chavePix, CancellationToken ct = default) =>
        db.Contas.AnyAsync(c => c.ChavePix == chavePix, ct);

    // SELECT ... FOR UPDATE: trava a linha até o COMMIT/ROLLBACK. Uma segunda transferência que toque
    // a mesma conta espera aqui, e então lê o saldo JÁ atualizado (sem "lost update" nem gasto duplo).
    // O EF Core não tem um equivalente ao @Lock(PESSIMISTIC_WRITE), então o SQL é escrito à mão.
    // FromSql com string interpolada vira parâmetro ($1), não concatenação: não há SQL injection.
    public async Task<Conta?> BuscarComLockPorChavePixAsync(string chavePix, CancellationToken ct = default)
    {
        if (db.Database.IsNpgsql())
        {
            var contas = await db.Contas
                .FromSql($"SELECT * FROM conta WHERE chave_pix = {chavePix} FOR UPDATE")
                .ToListAsync(ct);
            return contas.SingleOrDefault();
        }
        // SQLite (testes/desenvolvimento) não tem FOR UPDATE: ele trava o arquivo inteiro na escrita.
        // A prova de concorrência roda no PostgreSQL (PostgresTests e a ferramenta de carga em Go).
        return await db.Contas.SingleOrDefaultAsync(c => c.ChavePix == chavePix, ct);
    }

    public Task<long> ContarAsync(CancellationToken ct = default) => db.Contas.LongCountAsync(ct);

    public void Adicionar(Conta conta) => db.Contas.Add(conta);

    public void Remover(Conta conta) => db.Contas.Remove(conta);
}
