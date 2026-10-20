namespace ApiFoundation.Models;

public enum StatusNotificacao { INICIAL, PENDENTE, CONCLUIDO, FALHA }

public class NotificacaoLeomob
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public StatusNotificacao Status { get; set; }
    public int Tentativas { get; set; }
}