using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using PixApi.Api.Erros;
using PixApi.Infrastructure.Monitoramento;

namespace PixApi.Api.Controllers;

// Últimas linhas de log do servidor. Logs revelam detalhes internos (erros, endereços, chaves Pix),
// então o endpoint exige o cabeçalho X-Admin-Token igual a Pix:Admin:Token (ou PIX_ADMIN_TOKEN).
// Sem token configurado, o endpoint fica desativado (404).
[ApiController]
[Route("logs")]
public class LogsController(LogsEmMemoria logs, IConfiguration config) : ControllerBase
{
    private const int LimitePadrao = 200;
    private const int LimiteMaximo = 500;

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LinhaDeLog>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Ultimas([FromHeader(Name = "X-Admin-Token")] string? token, [FromQuery] int limite = LimitePadrao)
    {
        var esperado = Encoding.UTF8.GetBytes(config["PIX_ADMIN_TOKEN"] ?? config["Pix:Admin:Token"] ?? "");
        if (esperado.Length == 0)
        {
            return Problemas.Resultado(HttpContext, StatusCodes.Status404NotFound,
                "Defina a variável de ambiente PIX_ADMIN_TOKEN para habilitar este endpoint.", "Logs desativados");
        }
        // FixedTimeEquals compara em tempo constante: não "vaza", pela demora da resposta, quantos
        // caracteres do token o atacante acertou (ataque de timing). É o MessageDigest.isEqual do Java.
        if (token is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), esperado))
        {
            return Problemas.Resultado(HttpContext, StatusCodes.Status401Unauthorized,
                "Envie o token de administrador no cabeçalho X-Admin-Token.", "Token inválido");
        }
        return Ok(logs.Ultimas(Math.Clamp(limite, 1, LimiteMaximo)));
    }
}
