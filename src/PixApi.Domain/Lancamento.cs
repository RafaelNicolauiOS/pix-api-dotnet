namespace PixApi.Domain;

// Linha do razão. Imutável: sem setters públicos. Um erro contábil se corrige com um novo
// lançamento, nunca editando ou apagando o antigo.
public class Lancamento
{
    public long Id { get; private set; }
    public long ContaId { get; private set; }
    public TipoLancamento Tipo { get; private set; }
    public decimal Valor { get; private set; } // sempre positivo; o tipo define crédito ou débito
    public DateTimeOffset RealizadoEm { get; private set; }
    public Guid? TransferenciaId { get; private set; } // Guid = UUID

    // Usado pelo EF Core ao materializar do banco.
    private Lancamento()
    {
    }

    private Lancamento(long contaId, TipoLancamento tipo, decimal valor, DateTimeOffset realizadoEm, Guid? transferenciaId)
    {
        ContaId = contaId;
        Tipo = tipo;
        Valor = valor;
        RealizadoEm = realizadoEm;
        TransferenciaId = transferenciaId;
    }

    // "Static factory methods": nomes que dizem a intenção, como inits nomeados em Swift.
    public static Lancamento Abertura(long contaId, decimal valor, DateTimeOffset em) =>
        new(contaId, TipoLancamento.ABERTURA, valor, em, null);

    public static Lancamento PixEnviado(long contaId, decimal valor, DateTimeOffset em, Guid transferenciaId) =>
        new(contaId, TipoLancamento.PIX_ENVIADO, valor, em, transferenciaId);

    public static Lancamento PixRecebido(long contaId, decimal valor, DateTimeOffset em, Guid transferenciaId) =>
        new(contaId, TipoLancamento.PIX_RECEBIDO, valor, em, transferenciaId);

    public static Lancamento Encerramento(long contaId, decimal valor, DateTimeOffset em) =>
        new(contaId, TipoLancamento.ENCERRAMENTO, valor, em, null);

    // Crédito soma, débito subtrai: somar ValorComSinal() de uma conta dá o saldo dela.
    public decimal ValorComSinal() => Tipo.EhCredito() ? Valor : -Valor;
}
