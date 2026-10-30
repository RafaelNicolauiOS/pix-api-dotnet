using System.Data;

namespace PixApi.Application.Portas;

// Unit of Work: controla a transação e o "flush" das mudanças.
// No Spring isso era invisível (@Transactional + dirty checking); aqui o caso de uso é explícito:
// abre a transação, altera as entidades, salva e confirma. Sem ConfirmarAsync, o Dispose faz rollback.
public interface IUnidadeDeTrabalho
{
    Task<ITransacao> IniciarTransacaoAsync(IsolationLevel isolamento = IsolationLevel.ReadCommitted,
                                           CancellationToken ct = default);

    // Grava as mudanças detectadas nas entidades (o "dirty checking" do EF Core: ChangeTracker).
    // Lança ConflitoDeChaveUnicaException se o banco recusar por UNIQUE.
    Task SalvarAsync(CancellationToken ct = default);
}

// IAsyncDisposable: usado com "await using", que garante o rollback se a transação não for confirmada
// (parecido com o "defer" do Swift ou o try-with-resources do Java).
public interface ITransacao : IAsyncDisposable
{
    Task ConfirmarAsync(CancellationToken ct = default);
}
