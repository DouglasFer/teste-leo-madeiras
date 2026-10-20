namespace ApiFoundation.Integracoes;

public interface ILeomobApiCliente
{
    Task NotificarAsync(int pedidoSVL);
}