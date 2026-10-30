using System.ComponentModel.DataAnnotations;

namespace PixApi.Api.Dtos;

// Atributos de validação com as mesmas mensagens do Bean Validation em pt-BR (contrato do pix-api).
// Valor nulo é "válido" aqui: quem cobra presença é o [Required], como no Jakarta (@NotNull à parte).

public sealed class MaiorOuIgualAZeroAttribute() : ValidationAttribute("deve ser maior ou igual a 0")
{
    public override bool IsValid(object? valor) => valor is not decimal d || d >= 0;
}

public sealed class PositivoAttribute() : ValidationAttribute("deve ser maior que 0")
{
    public override bool IsValid(object? valor) => valor is not decimal d || d > 0;
}

// O @Digits(integer = 13, fraction = 2) do Java: recusa 0.001 (fração de centavo) e valores gigantes.
// "30.00" e "30.000" passam (zeros à direita não contam), como no Bean Validation.
public sealed class DigitosAttribute(int inteiros, int fracao)
    : ValidationAttribute($"valor numérico fora do limite (<{inteiros} dígitos>.<{fracao} dígitos> esperado)")
{
    private readonly decimal _limiteInteiro = (decimal)Math.Pow(10, inteiros);
    private readonly decimal _fatorFracao = (decimal)Math.Pow(10, fracao);

    public override bool IsValid(object? valor)
    {
        if (valor is not decimal d) return true;
        var deslocado = Math.Abs(d) * _fatorFracao;
        return deslocado == decimal.Truncate(deslocado) && Math.Abs(d) < _limiteInteiro;
    }
}
