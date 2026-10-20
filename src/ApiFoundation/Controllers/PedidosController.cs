using ApiFoundation.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiFoundation.Controllers;

[ApiController]
[Route("api/pedidos")]
public class PedidosController(ConsultaPedidos consultaPedidos) : ControllerBase
{
    private readonly ConsultaPedidos _consultaPedidos = consultaPedidos;

    [HttpGet("{pedidoSVL:int}")]
    public async Task<IActionResult> Obter(int pedidoSVL)
    {
        var pedido = await _consultaPedidos.ObterAsync(pedidoSVL);

        if (pedido == null)
        {
            return NotFound(new { mensagem = $"Pedido {pedidoSVL} não encontrado" });
        }

        return Ok(pedido);
    }
}