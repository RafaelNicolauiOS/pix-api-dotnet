using PixApi.Domain;

namespace PixApi.Api.Dtos;

// DTO de saída: a API não expõe a entidade diretamente (mudar a tabela não muda o contrato).
public sealed record ContaResponse(long Id, string Titular, string Cpf, string ChavePix, decimal Saldo)
{
    public static ContaResponse De(Conta conta) => new(conta.Id, conta.Titular, conta.Cpf, conta.ChavePix, conta.Saldo);
}
