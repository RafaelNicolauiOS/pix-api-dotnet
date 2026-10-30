using System.Data;
using PixApi.Application.Portas;
using PixApi.Domain;

namespace PixApi.Application.Batch;

// Gera o arquivo de movimento que o batch noturno em COBOL concilia (mesmo contrato do pix-api).
public class ExportacaoMovimentoService(IContaRepositorio contas, ILancamentoRepositorio lancamentos,
                                        IUnidadeDeTrabalho uow, TimeProvider relogio)
{
    // O "dia" de um banco brasileiro é o de Brasília, não o UTC do servidor.
    public static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private DateTime AgoraEmBrasilia(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, Fuso).DateTime;

    // O job roda de madrugada e fecha o dia que acabou.
    public DateOnly Ontem() => DateOnly.FromDateTime(AgoraEmBrasilia(relogio.GetUtcNow())).AddDays(-1);

    public async Task<IReadOnlyList<string>> ExportarAsync(DateOnly dataReferencia, CancellationToken ct = default)
    {
        var corte = relogio.GetUtcNow();
        if (dataReferencia > DateOnly.FromDateTime(AgoraEmBrasilia(corte)))
        {
            throw new DataDeReferenciaInvalidaException($"A data de referência não pode estar no futuro: {dataReferencia:yyyy-MM-dd}");
        }

        // REPEATABLE READ: saldos e razão saem da MESMA "foto" do banco. Sem isso, um Pix feito entre as
        // duas leituras apareceria no saldo mas não no razão, e o batch acusaria uma divergência que não existe.
        await using var transacao = await uow.IniciarTransacaoAsync(IsolationLevel.RepeatableRead, ct);

        var registros = new List<string> { LayoutMovimento.Header(dataReferencia, AgoraEmBrasilia(corte)) };

        var todasAsContas = await contas.ListarOrdenadoPorIdAsync(ct);
        registros.AddRange(todasAsContas.Select(c => LayoutMovimento.Conta(c.Id, c.ChavePix, c.Titular, c.Saldo)));

        // O razão inteiro até o corte: o COBOL separa o que é anterior ao dia (abertura), do dia e posterior.
        decimal creditos = 0, debitos = 0;
        var razao = await lancamentos.ListarAteAsync(corte, ct);
        foreach (var l in razao)
        {
            var credito = l.Tipo.EhCredito();
            registros.Add(LayoutMovimento.Lancamento(l.ContaId, AgoraEmBrasilia(l.RealizadoEm), credito,
                l.Tipo.ToString(), l.Valor, l.TransferenciaId));
            if (credito) creditos += l.Valor; else debitos += l.Valor;
        }

        registros.Add(LayoutMovimento.Trailer(todasAsContas.Count, razao.Count, creditos, debitos));
        await transacao.ConfirmarAsync(ct);
        return registros;
    }
}
