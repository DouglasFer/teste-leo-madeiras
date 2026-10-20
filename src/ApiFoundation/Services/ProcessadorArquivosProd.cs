using ApiFoundation.Data;
using ApiFoundation.Dtos;
using ApiFoundation.Integracoes;
using ApiFoundation.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiFoundation.Services;

public class ProcessadorArquivosProd(AppDbContext dbContext, ILogger<ProcessadorArquivosProd> logger, IArmazenamentoArquivos armazenamento, NotificadorLeomob notificador)
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ILogger<ProcessadorArquivosProd> _logger = logger;
    private readonly IArmazenamentoArquivos _armazenamento = armazenamento;
    private readonly NotificadorLeomob _notificador = notificador;

    public async Task<ArquivosProdResponse> ProcessarArquivosProd(ArquivosProdRequest request)
    {
        var resultados = new List<ResultadoServico>();
        foreach (var servico in request.Services)
        {
            var resultado = await ProcessarServico(servico);
            resultados.Add(resultado);
        }
        return new ArquivosProdResponse(resultados);
    }

    private async Task<ResultadoServico> ProcessarServico(ServicoRequest servico)
    {
        var pedido = await _dbContext.Pedidos.FirstOrDefaultAsync(p => p.PedidoSVL == servico.PedidoSVL);

        if (pedido == null)
        {
            _logger.LogWarning("Pedido não encontrado: {PedidoSVL}", servico.PedidoSVL);
            return new ResultadoServico(servico.ServiceId, servico.PedidoSVL, false, "Pedido não encontrado");
        }

        if (servico.ServiceId != pedido.ServiceId)
        {
            _logger.LogWarning("ServiceId divergente no pedido {PedidoSVL}: esperado {ServiceIdPedido}, recebido {ServiceIdRecebido}",
            pedido.PedidoSVL, pedido.ServiceId, servico.ServiceId);

            return new ResultadoServico(servico.ServiceId, servico.PedidoSVL, false, $"ServiceId divergente: o pedido {pedido.PedidoSVL} tem serviceId {pedido.ServiceId}");
        }

        var urlsExistentes = (await _dbContext.Links
            .Where(l => l.PedidoId == pedido.Id)
            .Select(l => l.Url)
            .ToListAsync()).ToHashSet();

        var links = ExtrairLinks(servico.Links);
        var novosLinks = new List<LinkArquivo>();
        foreach (var link in links)
        {
            if (urlsExistentes.Add(link.Url))
            {
                var novoLink = new LinkArquivo { PedidoId = pedido.Id, Categoria = link.Categoria, Url = link.Url };
                _dbContext.Links.Add(novoLink);
                novosLinks.Add(novoLink);
            }
        }

        if (pedido.TipoPedido == TipoPedido.ZLPL || pedido.TipoPedido == TipoPedido.ZCVS)
        {
            foreach (var link in novosLinks)
            {
                await _armazenamento.CopiarAsync(pedido.PedidoSVL, link.Url);
            }
        }

        NotificacaoLeomob? notificacao = null;
        if (pedido.TipoPedido == TipoPedido.ZMOB && novosLinks.Count > 0)
        {
            var notificado = await _dbContext.Notificacoes.AnyAsync(n => n.PedidoId == pedido.Id);

            if (!notificado)
            {
                notificacao = new NotificacaoLeomob { PedidoId = pedido.Id, Status = StatusNotificacao.INICIAL };
                _dbContext.Notificacoes.Add(notificacao);
            }
        }

        await _dbContext.SaveChangesAsync();

        if (notificacao != null)
        {
            await _notificador.TentarNotificarAsync(notificacao, pedido.PedidoSVL);
        }

        return new ResultadoServico(servico.ServiceId, servico.PedidoSVL, true, null);
    }

    private static List<(string Categoria, string Url)> ExtrairLinks(LinksRequest links)
    {
        var resultado = new List<(string Categoria, string Url)>();

        void Adicionar(string categoria, List<string>? urls)
        {
            foreach (var url in urls ?? new List<string>())
                resultado.Add((categoria, url));
        }

        Adicionar("corte.pdfSumario", links.Corte?.PdfSumario);
        Adicionar("corte.pdfDetalhado", links.Corte?.PdfDetalhado);
        Adicionar("etiquetas", links.Etiquetas);
        Adicionar("usinagem", links.Usinagem);

        return resultado;
    }
}