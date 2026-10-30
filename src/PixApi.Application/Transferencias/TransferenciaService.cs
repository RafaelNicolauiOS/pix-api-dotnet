using Microsoft.Extensions.Logging;
using PixApi.Application.Contas;
using PixApi.Application.Eventos;
using PixApi.Application.Portas;
using PixApi.Domain;

namespace PixApi.Application.Transferencias;

public class TransferenciaService(IContaRepositorio contas, ILancamentoRepositorio lancamentos, IUnidadeDeTrabalho uow,
                                  IPublicadorDeEventos publicador, TimeProvider relogio,
                                  ILogger<TransferenciaService> log)
{
    // Tudo entre IniciarTransacao e Confirmar é "tudo ou nada": qualquer exceção sai do método sem
    // ConfirmarAsync, e o "await using" faz rollback. É o @Transactional do Java, só que explícito.
    public async Task<ComprovanteTransferencia> TransferirAsync(string chaveOrigem, string chaveDestino, decimal valor,
                                                                CancellationToken ct = default)
    {
        if (chaveOrigem == chaveDestino)
        {
            throw new TransferenciaInvalidaException("Chave de origem e de destino devem ser diferentes");
        }

        TransferenciaRealizadaEvent evento;
        Conta origem;
        await using (var transacao = await uow.IniciarTransacaoAsync(ct: ct))
        {
            // As contas são travadas (SELECT ... FOR UPDATE) sempre na mesma ordem. Assim, A→B e B→A
            // simultâneas não ficam esperando uma pela outra para sempre (deadlock).
            // CompareOrdinal compara os códigos dos caracteres, como o String.compareTo do Java.
            // O string.Compare "normal" usa a cultura do servidor: a ordem poderia mudar entre máquinas.
            Conta destino;
            if (string.CompareOrdinal(chaveOrigem, chaveDestino) < 0)
            {
                origem = await BuscarComLockAsync(chaveOrigem, ct);
                destino = await BuscarComLockAsync(chaveDestino, ct);
            }
            else
            {
                destino = await BuscarComLockAsync(chaveDestino, ct);
                origem = await BuscarComLockAsync(chaveOrigem, ct);
            }

            origem.Debitar(valor); // lança SaldoInsuficienteException se não houver saldo
            destino.Creditar(valor);

            var agora = relogio.GetUtcNow();
            var id = Guid.NewGuid();

            // Partidas dobradas: todo débito tem um crédito de mesmo valor, ligados pelo id da transferência.
            lancamentos.Adicionar(Lancamento.PixEnviado(origem.Id, valor, agora, id));
            lancamentos.Adicionar(Lancamento.PixRecebido(destino.Id, valor, agora, id));

            await uow.SalvarAsync(ct);         // UPDATE dos 2 saldos + INSERT dos 2 lançamentos
            await transacao.ConfirmarAsync(ct); // COMMIT (libera os locks)

            evento = new TransferenciaRealizadaEvent(id, chaveOrigem, chaveDestino, valor, agora);
        }

        // Só chegamos aqui depois do COMMIT: nunca avisamos de uma transferência que sofreu rollback.
        // (No Spring, o @TransactionalEventListener(AFTER_COMMIT) fazia isso por baixo dos panos.)
        try
        {
            await publicador.PublicarAsync(evento, CancellationToken.None);
        }
        catch (Exception erro)
        {
            // A transferência já está gravada. Falhar aqui faria o cliente tentar de novo e pagar duas vezes.
            // O evento se perde: o padrão Transactional Outbox (ver README) resolve esse caso.
            log.LogError(erro, "Falha ao publicar evento {EventoId}", evento.Id);
        }

        return new ComprovanteTransferencia(chaveOrigem, chaveDestino, valor, origem.Saldo, evento.RealizadaEm);
    }

    private async Task<Conta> BuscarComLockAsync(string chavePix, CancellationToken ct) =>
        await contas.BuscarComLockPorChavePixAsync(chavePix, ct) ?? throw new ChavePixNaoEncontradaException(chavePix);
}
