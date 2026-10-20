namespace ApiFoundation.Integracoes;

public class LeomobApiCliente(double taxaFalha, ILogger<LeomobApiCliente> logger) : ILeomobApiCliente
{
    private readonly double _taxaFalha = taxaFalha;
    private readonly ILogger<LeomobApiCliente> _logger = logger;

    public int Chamadas { get; private set; }

    public Task NotificarAsync(int pedidoSVL)
    {
        Chamadas++;

        if (Random.Shared.NextDouble() < _taxaFalha)
        {
            _logger.LogWarning("LeomobAPI falhou ao receber o pedido {PedidoSVL}", pedidoSVL);
            throw new HttpRequestException($"LeomobAPI indisponível para o pedido {pedidoSVL}");
        }

        _logger.LogInformation("LeomobAPI notificada do pedido {PedidoSVL}", pedidoSVL);
        return Task.CompletedTask;
    }
}