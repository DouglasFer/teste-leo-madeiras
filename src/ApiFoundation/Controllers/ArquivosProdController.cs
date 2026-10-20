using ApiFoundation.Dtos;
using ApiFoundation.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiFoundation.Controllers;

[ApiController]
[Route("api/arquivos-producao")]
public class ArquivosProdController(ProcessadorArquivosProd processador) : ControllerBase
{
    private readonly ProcessadorArquivosProd _processador = processador;

    [HttpPost]
    public async Task<IActionResult> Receber(ArquivosProdRequest request)
    {
        if (request.HasErrors)
        {
            return UnprocessableEntity(new { mensagem = "Envio rejeitado: a Digibee sinalizou hasErrors = true. Nenhum serviço foi processado." });
        }

        var resposta = await _processador.ProcessarArquivosProd(request);
        return Ok(resposta);
    }
}