using Microsoft.EntityFrameworkCore;
using PixApi.Application.Portas;
using PixApi.Domain;

namespace PixApi.Infrastructure.Persistencia;

public class LancamentoRepositorio(PixDbContext db) : ILancamentoRepositorio
{
    public void Adicionar(Lancamento lancamento) => db.Lancamentos.Add(lancamento);

    public async Task<IReadOnlyList<Lancamento>> ListarAteAsync(DateTimeOffset corte, CancellationToken ct = default) =>
        await db.Lancamentos
            .Where(l => l.RealizadoEm <= corte)
            .OrderBy(l => l.RealizadoEm).ThenBy(l => l.Id)
            .AsNoTracking() // só leitura: não precisa rastrear mudanças
            .ToListAsync(ct);
}
