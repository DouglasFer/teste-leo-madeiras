namespace ApiFoundation.Dtos;

public record PedidoResponse(int PedidoSVL, long ServiceId, string TipoPedido, List<LinkResponse> Links, NotificacaoResponse? Notificacao);

public record LinkResponse(string Categoria, string Url);

public record NotificacaoResponse(string Status, int Tentativas);