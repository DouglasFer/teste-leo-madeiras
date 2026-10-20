using ApiFoundation.Data;
using ApiFoundation.Dtos;
using ApiFoundation.Integracoes;
using ApiFoundation.Models;
using ApiFoundation.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiFoundation.Tests;

public class ProcessadorArquivosProdTests : IDisposable
{
    private readonly SqliteConnection _conexao;
    private readonly AppDbContext _dbContext;
    private readonly ArmazenamentoArquivos _armazenamento;

    public ProcessadorArquivosProdTests()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).Options;
        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _armazenamento = new ArmazenamentoArquivos(NullLogger<ArmazenamentoArquivos>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _conexao.Dispose();
    }

    private (ProcessadorArquivosProd Processador, NotificadorLeomob Notificador, LeomobApiCliente Leomob) CriarProcessador(double taxaFalhaLeomob)
    {
        var leomob = new LeomobApiCliente(taxaFalhaLeomob, NullLogger<LeomobApiCliente>.Instance);
        var notificador = new NotificadorLeomob(_dbContext, leomob, NullLogger<NotificadorLeomob>.Instance);
        var processador = new ProcessadorArquivosProd(_dbContext, NullLogger<ProcessadorArquivosProd>.Instance, _armazenamento, notificador);
        return (processador, notificador, leomob);
    }

    private static ServicoRequest Servico(long serviceId, int pedidoSVL) => new(
        serviceId,
        pedidoSVL,
        new LinksRequest(
            new CorteRequest(
                [$"https://arquivos.exemplo.com/{pedidoSVL}/sumario.pdf"],
                [$"https://arquivos.exemplo.com/{pedidoSVL}/detalhado.pdf"]),
                [$"https://arquivos.exemplo.com/{pedidoSVL}/etiqueta-1.png"],
                [$"https://arquivos.exemplo.com/{pedidoSVL}/usinagem.xml"]));

    private static ArquivosProdRequest Envio(params ServicoRequest[] servicos) =>
        new("filesToProduction", false, servicos.ToList());

    [Fact]
    public async Task PedidoZmob_NaoAcionaArmazenamento()
    {
        var (processador, _, _) = CriarProcessador(taxaFalhaLeomob: 0);

        await processador.ProcessarArquivosProd(Envio(Servico(21763699, 1012)));

        Assert.Empty(_armazenamento.ArquivosCopiados);
    }

    [Fact]
    public async Task PedidoZlpl_AcionaArmazenamentoUmaVezPorArquivo()
    {
        var (processador, _, _) = CriarProcessador(taxaFalhaLeomob: 0);

        await processador.ProcessarArquivosProd(Envio(Servico(21763653, 1007)));

        Assert.Equal(4, _armazenamento.ArquivosCopiados.Count);
        Assert.Equal(4, _armazenamento.ArquivosCopiados.Distinct().Count());
    }

    [Fact]
    public async Task EnvioRepetido_NaoDuplicaLinksCopiasNemNotificacoes()
    {
        var (processador, _, leomob) = CriarProcessador(taxaFalhaLeomob: 0);
        var envio = Envio(Servico(21763653, 1007), Servico(21763699, 1012));

        await processador.ProcessarArquivosProd(envio);
        await processador.ProcessarArquivosProd(envio);

        Assert.Equal(8, await _dbContext.Links.CountAsync());
        Assert.Equal(4, _armazenamento.ArquivosCopiados.Count);
        Assert.Equal(1, await _dbContext.Notificacoes.CountAsync());
        Assert.Equal(1, leomob.Chamadas);
    }

    [Fact]
    public async Task Notificacao_VaiParaFalhaAposTerceiraTentativaSemSucesso()
    {
        var (processador, notificador, leomob) = CriarProcessador(taxaFalhaLeomob: 1);

        await processador.ProcessarArquivosProd(Envio(Servico(21763699, 1012)));
        var notificacao = await _dbContext.Notificacoes.SingleAsync();
        Assert.Equal(StatusNotificacao.PENDENTE, notificacao.Status);
        Assert.Equal(1, notificacao.Tentativas);

        await notificador.ReprocessarPendentesAsync();
        Assert.Equal(StatusNotificacao.PENDENTE, notificacao.Status);

        await notificador.ReprocessarPendentesAsync();
        Assert.Equal(StatusNotificacao.FALHA, notificacao.Status);
        Assert.Equal(3, notificacao.Tentativas);

        await notificador.ReprocessarPendentesAsync();
        Assert.Equal(3, leomob.Chamadas);
    }

    [Fact]
    public async Task EnvioComPedidoInexistente_ProcessaOsDemaisNormalmente()
    {
        var (processador, _, _) = CriarProcessador(taxaFalhaLeomob: 0);

        var resposta = await processador.ProcessarArquivosProd(Envio(Servico(99999999, 9999), Servico(21763653, 1007)));

        Assert.False(resposta.Resultados[0].Sucesso);
        Assert.Equal("Pedido não encontrado", resposta.Resultados[0].Erro);
        Assert.True(resposta.Resultados[1].Sucesso);
        Assert.Equal(4, await _dbContext.Links.CountAsync(l => l.PedidoId == 1));
    }
}