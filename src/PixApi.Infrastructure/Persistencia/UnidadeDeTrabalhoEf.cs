using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PixApi.Application.Portas;

namespace PixApi.Infrastructure.Persistencia;

public class UnidadeDeTrabalhoEf(PixDbContext db) : IUnidadeDeTrabalho
{
    public async Task<ITransacao> IniciarTransacaoAsync(IsolationLevel isolamento = IsolationLevel.ReadCommitted,
                                                        CancellationToken ct = default) =>
        new TransacaoEf(await db.Database.BeginTransactionAsync(isolamento, ct));

    public async Task SalvarAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException erro) when (ViolouUnicidade(erro))
        {
            throw new ConflitoDeChaveUnicaException(erro);
        }
    }

    // 23505 = unique_violation no PostgreSQL; 2067 = SQLITE_CONSTRAINT_UNIQUE.
    private static bool ViolouUnicidade(DbUpdateException erro) => erro.InnerException switch
    {
        PostgresException pg => pg.SqlState == PostgresErrorCodes.UniqueViolation,
        SqliteException sqlite => sqlite.SqliteExtendedErrorCode == 2067,
        _ => false,
    };

    private sealed class TransacaoEf(IDbContextTransaction transacao) : ITransacao
    {
        public Task ConfirmarAsync(CancellationToken ct = default) => transacao.CommitAsync(ct);

        // Sem Commit, o Dispose desfaz tudo (rollback) e libera os locks.
        public ValueTask DisposeAsync() => transacao.DisposeAsync();
    }
}
