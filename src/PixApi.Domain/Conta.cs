namespace PixApi.Domain;

// Entidade de domínio "pura": não conhece EF Core nem HTTP. No Java, @Entity/@Column ficavam na
// própria classe; aqui o mapeamento para a tabela fica na Infrastructure (ContaConfiguration).
public class Conta
{
    // "private set": só a própria classe altera (como "private(set) var" em Swift).
    // O EF Core consegue preencher mesmo assim, ao ler do banco.
    public long Id { get; private set; }
    public string Titular { get; private set; }
    public string Cpf { get; private set; }
    public string ChavePix { get; private set; }

    // decimal para dinheiro: base 10, 28 dígitos significativos. 0.1m + 0.2m == 0.3m (com double, não).
    // É o BigDecimal do Java, mas é um tipo primitivo da linguagem: operadores +, -, < funcionam direto.
    public decimal Saldo { get; private set; }

    // O EF Core usa este construtor ao ler do banco: casa os parâmetros com as propriedades pelo nome.
    public Conta(string titular, string cpf, string chavePix, decimal saldo)
    {
        Titular = titular;
        Cpf = cpf;
        ChavePix = chavePix;
        Saldo = saldo;
    }

    // Regra de negócio no domínio: saldo e CPF não mudam por atualização cadastral.
    public void AtualizarDados(string titular, string chavePix)
    {
        Titular = titular;
        ChavePix = chavePix;
    }

    // A própria conta protege a regra "não pode ficar negativa" (encapsulamento).
    public void Debitar(decimal valor)
    {
        if (Saldo < valor)
        {
            throw new SaldoInsuficienteException(ChavePix, valor);
        }
        Saldo -= valor;
    }

    public void Creditar(decimal valor) => Saldo += valor;
}
