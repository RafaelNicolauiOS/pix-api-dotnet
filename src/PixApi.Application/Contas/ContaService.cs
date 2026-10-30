using PixApi.Application.Portas;
using PixApi.Domain;

namespace PixApi.Application.Contas;

// Casos de uso de conta (o ContaService do Java).
// "Primary constructor" (C# 12): os parâmetros viram campos privados, como a injeção pelo construtor
// do Spring ou o init(...) de uma classe Swift. Quem cria a instância é o container de DI do ASP.NET.
public class ContaService(IContaRepositorio contas, ILancamentoRepositorio lancamentos, IUnidadeDeTrabalho uow,
                          TimeProvider relogio)
{
    public async Task<Conta> CriarAsync(Conta conta, CancellationToken ct = default)
    {
        // "await using": se algo der errado antes do ConfirmarAsync, o Dispose faz rollback.
        await using var transacao = await uow.IniciarTransacaoAsync(ct: ct);
        if (await contas.ExisteChavePixAsync(conta.ChavePix, ct))
        {
            throw new ChavePixDuplicadaException(conta.ChavePix);
        }
        try
        {
            contas.Adicionar(conta);
            await uow.SalvarAsync(ct); // INSERT: o banco gera o Id
            // Mesma transação: ou a conta e a abertura no razão são gravadas juntas, ou nada é.
            lancamentos.Adicionar(Lancamento.Abertura(conta.Id, conta.Saldo, relogio.GetUtcNow()));
            await uow.SalvarAsync(ct);
        }
        catch (ConflitoDeChaveUnicaException)
        {
            // Duas requisições com a mesma chave passaram juntas pelo ExisteChavePix; o UNIQUE barrou a 2ª.
            // (No Java essa corrida virava 500; aqui vira o mesmo 409 do caso comum.)
            throw new ChavePixDuplicadaException(conta.ChavePix);
        }
        await transacao.ConfirmarAsync(ct);
        return conta;
    }

    public async Task<Conta> BuscarPorIdAsync(long id, CancellationToken ct = default) =>
        await contas.BuscarPorIdAsync(id, ct) ?? throw new ContaNaoEncontradaException(id);

    public Task<IReadOnlyList<Conta>> ListarAsync(CancellationToken ct = default) =>
        contas.ListarOrdenadoPorIdAsync(ct);

    public async Task<Conta> AtualizarAsync(long id, string titular, string chavePix, CancellationToken ct = default)
    {
        var conta = await BuscarPorIdAsync(id, ct);
        var trocouChave = conta.ChavePix != chavePix;
        if (trocouChave && await contas.ExisteChavePixAsync(chavePix, ct))
        {
            throw new ChavePixDuplicadaException(chavePix);
        }
        conta.AtualizarDados(titular, chavePix);
        try
        {
            await uow.SalvarAsync(ct); // UPDATE das colunas alteradas (o EF detecta sozinho)
        }
        catch (ConflitoDeChaveUnicaException)
        {
            throw new ChavePixDuplicadaException(chavePix);
        }
        return conta;
    }

    public async Task RemoverAsync(long id, CancellationToken ct = default)
    {
        await using var transacao = await uow.IniciarTransacaoAsync(ct: ct);
        var conta = await BuscarPorIdAsync(id, ct);
        // O saldo restante "sai" no encerramento, zerando a conta no razão (o histórico fica).
        if (conta.Saldo > 0)
        {
            lancamentos.Adicionar(Lancamento.Encerramento(id, conta.Saldo, relogio.GetUtcNow()));
        }
        contas.Remover(conta);
        await uow.SalvarAsync(ct);
        await transacao.ConfirmarAsync(ct);
    }
}
