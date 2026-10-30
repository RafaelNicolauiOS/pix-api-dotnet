using PixApi.Domain;

namespace PixApi.Application.Portas;

// "Porta" de saída: a Application diz O QUE precisa do banco; a Infrastructure diz COMO (EF Core).
// Equivale ao ContaRepository do Java, mas sem herdar de JpaRepository: a interface é nossa,
// e a camada de casos de uso não depende do EF Core (ela nem referencia o pacote).
public interface IContaRepositorio
{
    Task<Conta?> BuscarPorIdAsync(long id, CancellationToken ct = default);

    // Sem ORDER BY o PostgreSQL devolve as linhas em qualquer ordem.
    Task<IReadOnlyList<Conta>> ListarOrdenadoPorIdAsync(CancellationToken ct = default);

    Task<bool> ExisteChavePixAsync(string chavePix, CancellationToken ct = default);

    // SELECT ... FOR UPDATE: trava a linha até o fim da transação.
    Task<Conta?> BuscarComLockPorChavePixAsync(string chavePix, CancellationToken ct = default);

    Task<long> ContarAsync(CancellationToken ct = default);

    void Adicionar(Conta conta);

    void Remover(Conta conta);
}
