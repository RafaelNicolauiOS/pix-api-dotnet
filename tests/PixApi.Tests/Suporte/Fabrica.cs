using PixApi.Domain;

namespace PixApi.Tests.Suporte;

public static class Fabrica
{
    // O Id normalmente é gerado pelo banco. Reflexão aqui equivale ao ReflectionTestUtils.setField do Java.
    public static PixApi.Domain.Conta ComId(this PixApi.Domain.Conta conta, long id)
    {
        typeof(PixApi.Domain.Conta).GetProperty(nameof(PixApi.Domain.Conta.Id))!.SetValue(conta, id);
        return conta;
    }

    public static PixApi.Domain.Conta Conta(string chave, decimal saldo, long id = 0) =>
        new PixApi.Domain.Conta("Titular " + chave, "12345678901", chave, saldo).ComId(id);
}
