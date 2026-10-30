using Microsoft.AspNetCore.Mvc;
using PixApi.Api.Dtos;
using PixApi.Application.Contas;

namespace PixApi.Api.Controllers;

// [ApiController]: valida o corpo automaticamente (400 antes de entrar no método) e lê JSON do corpo.
// É o @RestController + @Valid do Spring. [Route] define o prefixo (@RequestMapping).
[ApiController]
[Route("contas")]
public class ContasController(ContaService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ContaResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ContaResponse>> Criar(ContaRequest request, CancellationToken ct)
    {
        var criada = await service.CriarAsync(request.ParaEntidade(), ct);
        // 201 + cabeçalho Location: http://host/contas/{id} (o ServletUriComponentsBuilder do Java).
        return CreatedAtAction(nameof(Buscar), new { id = criada.Id }, ContaResponse.De(criada));
    }

    [HttpGet]
    public async Task<IEnumerable<ContaResponse>> Listar(CancellationToken ct) =>
        (await service.ListarAsync(ct)).Select(ContaResponse.De);

    [HttpGet("{id}")]
    [ProducesResponseType<ContaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ContaResponse> Buscar(long id, CancellationToken ct) =>
        ContaResponse.De(await service.BuscarPorIdAsync(id, ct));

    // Altera titular e chave; CPF e saldo do corpo são validados mas ignorados (igual ao Java).
    [HttpPut("{id}")]
    [ProducesResponseType<ContaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ContaResponse> Atualizar(long id, ContaRequest request, CancellationToken ct) =>
        ContaResponse.De(await service.AtualizarAsync(id, request.Titular!, request.ChavePix!, ct));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Remover(long id, CancellationToken ct)
    {
        await service.RemoverAsync(id, ct);
        return NoContent();
    }
}
