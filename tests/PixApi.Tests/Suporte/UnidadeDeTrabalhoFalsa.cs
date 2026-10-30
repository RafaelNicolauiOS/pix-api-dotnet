using System.Data;
using PixApi.Application.Portas;

namespace PixApi.Tests.Suporte;

// Um "fake" escrito à mão (em vez de um mock) porque o que interessa é a ORDEM das chamadas:
// salvar → confirmar → publicar. Tudo é registrado na lista compartilhada "Chamadas".
public sealed class UnidadeDeTrabalhoFalsa(List<string> chamadas) : IUnidadeDeTrabalho
{
    public IsolationLevel? Isolamento { get; private set; }
    public Exception? ErroAoSalvar { get; set; }
    public bool Confirmada { get; private set; }

    // Executado a cada SalvarAsync: simula o banco gerando ids (IDENTITY).
    public Action? AoSalvar { get; set; }

    public Task<ITransacao> IniciarTransacaoAsync(IsolationLevel isolamento = IsolationLevel.ReadCommitted,
                                                  CancellationToken ct = default)
    {
        Isolamento = isolamento;
        chamadas.Add("iniciar");
        return Task.FromResult<ITransacao>(new Transacao(this, chamadas));
    }

    public Task SalvarAsync(CancellationToken ct = default)
    {
        if (ErroAoSalvar is not null)
        {
            throw ErroAoSalvar;
        }
        AoSalvar?.Invoke();
        chamadas.Add("salvar");
        return Task.CompletedTask;
    }

    private sealed class Transacao(UnidadeDeTrabalhoFalsa uow, List<string> chamadas) : ITransacao
    {
        public Task ConfirmarAsync(CancellationToken ct = default)
        {
            uow.Confirmada = true;
            chamadas.Add("confirmar");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
