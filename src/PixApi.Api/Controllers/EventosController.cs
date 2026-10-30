using Microsoft.AspNetCore.Mvc;
using PixApi.Application.Eventos;
using PixApi.Application.Monitoramento;

namespace PixApi.Api.Controllers;

// Feed público com as últimas 100 transferências confirmadas (mais recente primeiro).
// Mostra só os dados do evento, que já são visíveis em GET /contas: nada interno é exposto.
[ApiController]
[Route("eventos")]
public class EventosController(HistoricoDeEventos historico) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<TransferenciaRealizadaEvent> Recentes() => historico.Recentes();
}
