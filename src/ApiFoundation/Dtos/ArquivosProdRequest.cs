namespace ApiFoundation.Dtos;

public record ArquivosProdRequest(string DocumentType, bool HasErrors, List<ServicoRequest> Services);
public record ServicoRequest(long ServiceId, int PedidoSVL, LinksRequest Links);
public record LinksRequest(CorteRequest? Corte, List<string>? Etiquetas, List<string>? Usinagem);
public record CorteRequest(List<string>? PdfSumario, List<string>? PdfDetalhado);