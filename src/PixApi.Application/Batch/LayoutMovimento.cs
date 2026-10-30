namespace PixApi.Application.Batch;

public static class LayoutMovimento
{
    public const int TamanhoRegistro = 150;

    public static string Header(DateOnly dataReferencia, DateTime geradoEm) => throw new NotImplementedException();
    public static string Conta(long id, string chavePix, string titular, decimal saldo) => throw new NotImplementedException();
    public static string Lancamento(long contaId, DateTime quando, bool credito, string tipo, decimal valor, Guid? transferenciaId) => throw new NotImplementedException();
    public static string Trailer(long qtdContas, long qtdLancamentos, decimal creditos, decimal debitos) => throw new NotImplementedException();
}
