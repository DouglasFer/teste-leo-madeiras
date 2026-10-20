namespace ApiFoundation.Dtos;

public record ResultadoServico(long ServiceId, int PedidoSVL, bool Sucesso, string? Erro);
public record ArquivosProdResponse(List<ResultadoServico> Resultados);