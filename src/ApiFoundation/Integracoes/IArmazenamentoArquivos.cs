namespace ApiFoundation.Integracoes;

public interface IArmazenamentoArquivos
{
    Task CopiarAsync(int pedidoSVL, string url);
}