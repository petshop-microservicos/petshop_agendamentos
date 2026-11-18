using Agendamentos.Api.DTOs;
using Agendamentos.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agendamentos.Api.Controllers;

[ApiController]
[Route("api/profissionais")]
public class ProfissionaisController(IProfissionaisService service) : ApiControllerBase
{
    // ---------- Profissionais (RF08) ----------

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProfissionalResponseDto>>> Listar(
        [FromQuery] ProfissionalFiltroDto filtro, CancellationToken ct)
        => Ok(await service.ListarAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProfissionalResponseDto>> ObterPorId(int id, CancellationToken ct)
    {
        var profissional = await service.ObterPorIdAsync(id, ct);
        return profissional is null ? NotFound() : Ok(profissional);
    }

    [HttpPost]
    public async Task<ActionResult<ProfissionalResponseDto>> Criar(
        ProfissionalCreateDto dto, CancellationToken ct)
    {
        var criado = await service.CriarAsync(dto, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProfissionalResponseDto>> Atualizar(
        int id, ProfissionalUpdateDto dto, CancellationToken ct)
    {
        var atualizado = await service.AtualizarAsync(id, dto, ct);
        return atualizado is null ? NotFound() : Ok(atualizado);
    }

    // Inativa ou reativa o profissional (não há exclusão: RN29)
    [HttpPatch("{id:int}/ativo")]
    public async Task<ActionResult<ProfissionalResponseDto>> AlterarAtivo(
        int id, AlterarAtivoDto dto, CancellationToken ct)
    {
        var profissional = await service.AlterarAtivoAsync(id, dto.Ativo!.Value, ct);
        return profissional is null ? NotFound() : Ok(profissional);
    }

    // ---------- Disponibilidade semanal (RF09) ----------

    [HttpGet("{id:int}/disponibilidade")]
    public async Task<ActionResult<IReadOnlyList<FaixaDisponibilidadeResponseDto>>> ObterDisponibilidade(
        int id, CancellationToken ct)
    {
        var faixas = await service.ObterDisponibilidadeAsync(id, ct);
        return faixas is null ? NotFound() : Ok(faixas);
    }

    // Substitui a grade semanal inteira
    [HttpPut("{id:int}/disponibilidade")]
    public async Task<ActionResult<IReadOnlyList<FaixaDisponibilidadeResponseDto>>> DefinirDisponibilidade(
        int id, DefinirDisponibilidadeDto dto, CancellationToken ct)
    {
        var faixas = await service.DefinirDisponibilidadeAsync(id, dto, ct);
        return faixas is null ? NotFound() : Ok(faixas);
    }

    // ---------- Serviços executados (RF10) ----------

    [HttpGet("{id:int}/servicos")]
    public async Task<ActionResult<IReadOnlyList<ServicoResponseDto>>> ListarServicos(
        int id, CancellationToken ct)
    {
        var servicos = await service.ListarServicosAsync(id, ct);
        return servicos is null ? NotFound() : Ok(servicos);
    }

    // Substitui o conjunto de serviços vinculados ao profissional
    [HttpPut("{id:int}/servicos")]
    public async Task<ActionResult<IReadOnlyList<ServicoResponseDto>>> DefinirServicos(
        int id, DefinirServicosDto dto, CancellationToken ct)
    {
        var resultado = await service.DefinirServicosAsync(id, dto, ct);

        if (!resultado.Sucesso)
            return Erro(resultado.Status, resultado.Mensagem);

        return Ok(resultado.Valor);
    }
}
