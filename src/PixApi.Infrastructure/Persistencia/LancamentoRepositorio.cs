using PixApi.Application.Portas;
using PixApi.Domain;

namespace PixApi.Infrastructure.Persistencia;

public class LancamentoRepositorio(PixDbContext db) : ILancamentoRepositorio
{
    public void Adicionar(Lancamento lancamento) => throw new NotImplementedException();
    public Task<IReadOnlyList<Lancamento>> ListarAteAsync(DateTimeOffset corte, CancellationToken ct = default) => throw new NotImplementedException();
}
