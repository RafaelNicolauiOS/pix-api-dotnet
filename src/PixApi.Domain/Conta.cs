namespace PixApi.Domain;

public class Conta
{
    public long Id { get; private set; }
    public string Titular { get; private set; }
    public string Cpf { get; private set; }
    public string ChavePix { get; private set; }
    public decimal Saldo { get; private set; }

    public Conta(string titular, string cpf, string chavePix, decimal saldo)
    {
        Titular = titular;
        Cpf = cpf;
        ChavePix = chavePix;
        Saldo = saldo;
    }

    public void AtualizarDados(string titular, string chavePix) => throw new NotImplementedException();
    public void Debitar(decimal valor) => throw new NotImplementedException();
    public void Creditar(decimal valor) => throw new NotImplementedException();
}
