namespace ApiFoundation.Models;

public enum TipoPedido { ZLPL, ZCVS, ZMOB }

public class Pedido
{
    public int Id { get; set; }
    public int PedidoSVL { get; set; }
    public long ServiceId { get; set; }
    public TipoPedido TipoPedido { get; set; }

    public List<LinkArquivo> Links { get; set; } = new();
    public NotificacaoLeomob? Notificacao { get; set; }
}