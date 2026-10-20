using ApiFoundation.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiFoundation.Controllers;

[ApiController]
[Route("api/notificacoes")]
public class NotificacoesController(NotificadorLeomob notificador) : ControllerBase
{
    private readonly NotificadorLeomob _notificador = notificador;

    [HttpPost("reprocessar")]
    public async Task<IActionResult> Reprocessar()
    {
        var quantidade = await _notificador.ReprocessarPendentesAsync();
        return Ok(new { notificacoesReprocessadas = quantidade });
    }
}