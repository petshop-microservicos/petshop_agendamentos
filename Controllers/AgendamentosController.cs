using Agendamentos.Api.DTOs;
using Agendamentos.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agendamentos.Api.Controllers;

[ApiController]
[Route("api/agendamentos")]
public class AgendamentosController(IAgendamentosService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AgendamentoResponseDto>>> Listar(
        [FromQuery] AgendamentoFiltroDto filtro, CancellationToken ct)
        => Ok(await service.ListarAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AgendamentoResponseDto>> ObterPorId(int id, CancellationToken ct)
    {
        var agendamento = await service.ObterPorIdAsync(id, ct);
        return agendamento is null ? NotFound() : Ok(agendamento);
    }

    [HttpPost]
    public async Task<ActionResult<AgendamentoResponseDto>> Criar(
        AgendamentoCreateDto dto, CancellationToken ct)
    {
        var resultado = await service.CriarAsync(dto, ct);

        if (!resultado.Sucesso)
            return Erro(resultado.Status, resultado.Mensagem);

        return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Valor!.Id }, resultado.Valor);
    }

    // Só agendamentos com status Agendado podem ser remarcados (RN06)
    [HttpPatch("{id:int}/remarcar")]
    public async Task<ActionResult<AgendamentoResponseDto>> Remarcar(
        int id, RemarcarAgendamentoDto dto, CancellationToken ct)
    {
        var resultado = await service.RemarcarAsync(id, dto, ct);

        if (!resultado.Sucesso)
            return Erro(resultado.Status, resultado.Mensagem);

        return Ok(resultado.Valor);
    }

    // Registro de andamento pelo funcionário: EmAndamento, Concluido, Cancelado ou Faltou (RN06, RN07)
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<AgendamentoResponseDto>> AlterarStatus(
        int id, AlterarStatusAgendamentoDto dto, CancellationToken ct)
    {
        var resultado = await service.AlterarStatusAsync(id, dto, ct);

        if (!resultado.Sucesso)
            return Erro(resultado.Status, resultado.Mensagem);

        return Ok(resultado.Valor);
    }
}
