namespace PixApi.Domain;

// Os nomes ficam em MAIÚSCULAS porque são gravados como texto no banco e no arquivo do COBOL,
// iguais aos do enum Java (contrato compartilhado).
public enum TipoLancamento
{
    ABERTURA,      // depósito inicial ao criar a conta
    PIX_RECEBIDO,
    PIX_ENVIADO,
    ENCERRAMENTO,  // saída do saldo restante ao excluir a conta
}

// Enums em C# não têm propriedades (em Java e em Swift têm). Um método de extensão faz o papel
// da computed property "isCredito".
public static class TipoLancamentoExtensions
{
    public static bool EhCredito(this TipoLancamento tipo) =>
        tipo is TipoLancamento.ABERTURA or TipoLancamento.PIX_RECEBIDO;
}
