using PixApi.Domain;

namespace PixApi.Tests.Domain;

// Unidade pura: sem banco, sem HTTP, sem mocks (equivale ao ContaTest do Java).
public class ContaTests
{
    private static Conta NovaConta(decimal saldo) => new("Ana Souza", "11111111111", "ana@pix.com", saldo);

    [Fact]
    public void Debitar_DeveSubtrairDoSaldo()
    {
        var conta = NovaConta(100.00m);

        conta.Debitar(30.50m);

        Assert.Equal(69.50m, conta.Saldo);
    }

    [Fact]
    public void Creditar_DeveSomarAoSaldo()
    {
        var conta = NovaConta(100.00m);

        conta.Creditar(0.10m);
        conta.Creditar(0.20m);

        Assert.Equal(100.30m, conta.Saldo); // com double seria 100.30000000000001
    }

    [Fact]
    public void Debitar_SemSaldo_DeveLancarExcecao_ESaldoNaoMuda()
    {
        var conta = NovaConta(50.00m);

        var erro = Assert.Throws<SaldoInsuficienteException>(() => conta.Debitar(50.01m));

        Assert.Equal("Saldo insuficiente na chave ana@pix.com para transferir R$ 50.01", erro.Message);
        Assert.Equal(50.00m, conta.Saldo);
    }

    [Fact]
    public void Debitar_ExatamenteOSaldo_DeveZerar()
    {
        var conta = NovaConta(50.00m);

        conta.Debitar(50.00m);

        Assert.Equal(0m, conta.Saldo);
    }

    [Fact]
    public void AtualizarDados_DeveTrocarTitularEChave_MasNaoSaldoNemCpf()
    {
        var conta = NovaConta(10m);

        conta.AtualizarDados("Ana S. Lima", "ana.lima@pix.com");

        Assert.Equal("Ana S. Lima", conta.Titular);
        Assert.Equal("ana.lima@pix.com", conta.ChavePix);
        Assert.Equal("11111111111", conta.Cpf);
        Assert.Equal(10m, conta.Saldo);
    }
}
