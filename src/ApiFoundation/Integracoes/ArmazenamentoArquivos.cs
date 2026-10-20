namespace ApiFoundation.Integracoes;

public class ArmazenamentoArquivos(ILogger<ArmazenamentoArquivos> _logger) : IArmazenamentoArquivos
{
    public List<string> ArquivosCopiados { get; } = new();

    public Task CopiarAsync(int pedidoSVL, string url)
    {
        ArquivosCopiados.Add(url);
        _logger.LogInformation("Pedido {PedidoSVL}: copiando {Url} para o armazenamento interno", pedidoSVL, url);
        return Task.CompletedTask;
    }
}