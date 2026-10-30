namespace PixApi.Domain;

public class Lancamento
{
    public long Id { get; private set; }
    public long ContaId { get; private set; }
    public TipoLancamento Tipo { get; private set; }
    public decimal Valor { get; private set; }
    public DateTimeOffset RealizadoEm { get; private set; }
    public Guid? TransferenciaId { get; private set; }

    public static Lancamento Abertura(long contaId, decimal valor, DateTimeOffset em) => throw new NotImplementedException();
    public static Lancamento PixEnviado(long contaId, decimal valor, DateTimeOffset em, Guid transferenciaId) => throw new NotImplementedException();
    public static Lancamento PixRecebido(long contaId, decimal valor, DateTimeOffset em, Guid transferenciaId) => throw new NotImplementedException();
    public static Lancamento Encerramento(long contaId, decimal valor, DateTimeOffset em) => throw new NotImplementedException();
    public decimal ValorComSinal() => throw new NotImplementedException();
}
