using PixApi.Application.Portas;

namespace PixApi.Application.Batch;

public class ExportacaoMovimentoService(IContaRepositorio contas, ILancamentoRepositorio lancamentos,
                                        IUnidadeDeTrabalho uow, TimeProvider relogio)
{
    public static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public DateOnly Ontem() => throw new NotImplementedException();
    public Task<IReadOnlyList<string>> ExportarAsync(DateOnly dataReferencia, CancellationToken ct = default) => throw new NotImplementedException();
}
