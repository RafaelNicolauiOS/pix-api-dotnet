using System.ComponentModel.DataAnnotations;
using PixApi.Domain;

namespace PixApi.Api.Dtos;

// DTO de entrada (o record ContaRequest do Java). As anotações são checadas automaticamente
// pelo [ApiController] antes de o método do controller rodar (o @Valid do Spring).
// Tipos "?" de propósito: sem eles, um campo ausente viraria "" ou 0 e passaria despercebido.
public sealed record ContaRequest
{
    [Required(ErrorMessage = "não deve estar em branco")] // [Required] em string recusa "" e "   " (= @NotBlank)
    public string? Titular { get; init; }

    [Required(ErrorMessage = "não deve estar em branco")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "deve conter 11 dígitos")]
    public string? Cpf { get; init; }

    [Required(ErrorMessage = "não deve estar em branco")]
    public string? ChavePix { get; init; }

    [Required(ErrorMessage = "não deve ser nulo")]
    [MaiorOuIgualAZero]
    [Digitos(13, 2)]
    public decimal? Saldo { get; init; }

    // Depois da validação os "!" são seguros: o [ApiController] já devolveu 400 se faltasse algo.
    public Conta ParaEntidade() => new(Titular!, Cpf!, ChavePix!, Saldo!.Value);
}
