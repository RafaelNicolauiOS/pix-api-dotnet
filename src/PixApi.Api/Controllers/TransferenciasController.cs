using Microsoft.AspNetCore.Mvc;
using PixApi.Api.Dtos;
using PixApi.Application.Transferencias;

namespace PixApi.Api.Controllers;

[ApiController]
[Route("transferencias")]
public class TransferenciasController(TransferenciaService service) : ControllerBase
{
    // 200 com o comprovante. Os erros viram ProblemDetails no ApiExceptionFilter.
    [HttpPost]
    [ProducesResponseType<ComprovanteTransferencia>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<ComprovanteTransferencia> Transferir(TransferenciaRequest request, CancellationToken ct) =>
        service.TransferirAsync(request.ChaveOrigem!, request.ChaveDestino!, request.Valor!.Value, ct);
}
