using Microsoft.Extensions.Logging;
using PixApi.Application.Portas;

namespace PixApi.Application.Transferencias;

public class TransferenciaService(IContaRepositorio contas, ILancamentoRepositorio lancamentos, IUnidadeDeTrabalho uow,
                                  IPublicadorDeEventos publicador, TimeProvider relogio,
                                  ILogger<TransferenciaService> log)
{
    public Task<ComprovanteTransferencia> TransferirAsync(string chaveOrigem, string chaveDestino, decimal valor,
                                                          CancellationToken ct = default) => throw new NotImplementedException();
}
