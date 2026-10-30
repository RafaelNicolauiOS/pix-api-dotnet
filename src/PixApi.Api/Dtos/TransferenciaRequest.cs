using System.ComponentModel.DataAnnotations;

namespace PixApi.Api.Dtos;

public sealed record TransferenciaRequest
{
    [Required(ErrorMessage = "não deve estar em branco")]
    public string? ChaveOrigem { get; init; }

    [Required(ErrorMessage = "não deve estar em branco")]
    public string? ChaveDestino { get; init; }

    [Required(ErrorMessage = "não deve ser nulo")]
    [Positivo]
    [Digitos(13, 2)]
    public decimal? Valor { get; init; }
}
