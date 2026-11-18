using Agendamentos.Api.DTOs;
using Agendamentos.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agendamentos.Api.Controllers;

[ApiController]
[Route("api/servicos")]
public class ServicosController(IServicosService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ServicoResponseDto>>> Listar(
        [FromQuery] ServicoFiltroDto filtro, CancellationToken ct)
        => Ok(await service.ListarAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ServicoResponseDto>> ObterPorId(int id, CancellationToken ct)
    {
        var servico = await service.ObterPorIdAsync(id, ct);
        return servico is null ? NotFound() : Ok(servico);
    }

    [HttpPost]
    public async Task<ActionResult<ServicoResponseDto>> Criar(
        ServicoCreateDto dto, CancellationToken ct)
    {
        var resultado = await service.CriarAsync(dto, ct);

        if (!resultado.Sucesso)
            return Erro(resultado.Status, resultado.Mensagem);

        return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Valor!.Id }, resultado.Valor);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ServicoResponseDto>> Atualizar(
        int id, ServicoUpdateDto dto, CancellationToken ct)
    {
        var resultado = await service.AtualizarAsync(id, dto, ct);

        if (!resultado.Sucesso)
            return Erro(resultado.Status, resultado.Mensagem);

        return Ok(resultado.Valor);
    }

    // Inativa ou reativa o serviço (não há exclusão: RN29)
    [HttpPatch("{id:int}/ativo")]
    public async Task<ActionResult<ServicoResponseDto>> AlterarAtivo(
        int id, AlterarAtivoDto dto, CancellationToken ct)
    {
        var servico = await service.AlterarAtivoAsync(id, dto.Ativo!.Value, ct);
        return servico is null ? NotFound() : Ok(servico);
    }
}
