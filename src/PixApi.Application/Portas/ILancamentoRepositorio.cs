using PixApi.Domain;

namespace PixApi.Application.Portas;

public interface ILancamentoRepositorio
{
    void Adicionar(Lancamento lancamento);

    // Razão completo até o instante de corte, em ordem cronológica (e por id no empate).
    Task<IReadOnlyList<Lancamento>> ListarAteAsync(DateTimeOffset corte, CancellationToken ct = default);
}
