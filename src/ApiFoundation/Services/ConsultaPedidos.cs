using ApiFoundation.Data;
using ApiFoundation.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ApiFoundation.Services;

public class ConsultaPedidos(AppDbContext dbContext)
{
    private readonly AppDbContext _dbContext = dbContext;

    public async Task<PedidoResponse?> ObterAsync(int pedidoSVL)
    {
        var pedido = await _dbContext.Pedidos
            .Include(p => p.Links)
            .Include(p => p.Notificacao)
            .FirstOrDefaultAsync(p => p.PedidoSVL == pedidoSVL);

        if (pedido == null)
        {
            return null;
        }

        return new PedidoResponse(
            pedido.PedidoSVL,
            pedido.ServiceId,
            pedido.TipoPedido.ToString(),
            pedido.Links.Select(l => new LinkResponse(l.Categoria, l.Url)).ToList(),
            pedido.Notificacao == null ? null : new NotificacaoResponse(pedido.Notificacao.Status.ToString(), pedido.Notificacao.Tentativas));
    }
}