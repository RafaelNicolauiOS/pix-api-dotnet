using PixApi.Application.Portas;
using PixApi.Domain;

namespace PixApi.Application.Contas;

public class ContaService(IContaRepositorio contas, ILancamentoRepositorio lancamentos, IUnidadeDeTrabalho uow,
                          TimeProvider relogio)
{
    public Task<Conta> CriarAsync(Conta conta, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Conta> BuscarPorIdAsync(long id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Conta>> ListarAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Conta> AtualizarAsync(long id, string titular, string chavePix, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoverAsync(long id, CancellationToken ct = default) => throw new NotImplementedException();
}
