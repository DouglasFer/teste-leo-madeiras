namespace ApiFoundation.Models;

public class LinkArquivo
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public string Categoria { get; set; } = "";
    public string Url { get; set; } = "";
}