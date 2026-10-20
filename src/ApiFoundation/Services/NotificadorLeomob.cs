using ApiFoundation.Data;
using ApiFoundation.Integracoes;
using ApiFoundation.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiFoundation.Services;

public class NotificadorLeomob(AppDbContext dbContext, ILeomobApiCliente leomob, ILogger<NotificadorLeomob> logger)
{
    public const int MaxTentativas = 3;

    private readonly AppDbContext _dbContext = dbContext;
    private readonly ILeomobApiCliente _leomob = leomob;
    private readonly ILogger<NotificadorLeomob> _logger = logger;

    public async Task TentarNotificarAsync(NotificacaoLeomob notificacao, int pedidoSVL)
    {
        try
        {
            await _leomob.NotificarAsync(pedidoSVL);
            notificacao.Status = StatusNotificacao.CONCLUIDO;
            _logger.LogInformation("Pedido {PedidoSVL}: notificação à LeomobAPI concluída", pedidoSVL);
        }
        catch (Exception ex)
        {
            notificacao.Tentativas++;
            notificacao.Status = notificacao.Tentativas >= MaxTentativas
                ? StatusNotificacao.FALHA
                : StatusNotificacao.PENDENTE;

            _logger.LogWarning(ex, "Pedido {PedidoSVL}: falha ao notificar a LeomobAPI (tentativa {Tentativas}), status {Status}",
                pedidoSVL, notificacao.Tentativas, notificacao.Status);
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task<int> ReprocessarPendentesAsync()
    {
        var pedidos = await _dbContext.Pedidos
            .Include(p => p.Notificacao)
            .Where(p => p.Notificacao != null && p.Notificacao.Status == StatusNotificacao.PENDENTE)
            .ToListAsync();

        foreach (var pedido in pedidos)
        {
            await TentarNotificarAsync(pedido.Notificacao!, pedido.PedidoSVL);
        }

        return pedidos.Count;
    }
}