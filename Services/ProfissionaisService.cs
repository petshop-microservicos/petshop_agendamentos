using Agendamentos.Api.Data;
using Agendamentos.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Agendamentos.Api.Services;

public interface IProfissionaisService
{
    Task<PagedResult<ProfissionalResponseDto>> ListarAsync(ProfissionalFiltroDto filtro, CancellationToken ct = default);
    Task<ProfissionalResponseDto?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<ProfissionalResponseDto> CriarAsync(ProfissionalCreateDto dto, CancellationToken ct = default);
    Task<ProfissionalResponseDto?> AtualizarAsync(int id, ProfissionalUpdateDto dto, CancellationToken ct = default);

    // Inativa ou reativa. Retorna null se o profissional não existir.
    Task<ProfissionalResponseDto?> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default);

    // Disponibilidade semanal (RF09). Retornam null se o profissional não existir.
    Task<IReadOnlyList<FaixaDisponibilidadeResponseDto>?> ObterDisponibilidadeAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<FaixaDisponibilidadeResponseDto>?> DefinirDisponibilidadeAsync(int id, DefinirDisponibilidadeDto dto, CancellationToken ct = default);

    // Serviços executados pelo profissional (RF10).
    Task<IReadOnlyList<ServicoResponseDto>?> ListarServicosAsync(int id, CancellationToken ct = default);
    Task<Resultado<IReadOnlyList<ServicoResponseDto>>> DefinirServicosAsync(int id, DefinirServicosDto dto, CancellationToken ct = default);
}

public class ProfissionaisService(AppDbContext db) : IProfissionaisService
{
    public async Task<PagedResult<ProfissionalResponseDto>> ListarAsync(
        ProfissionalFiltroDto filtro, CancellationToken ct = default)
    {
        var (page, pageSize) = filtro.Normalizado();

        var query = db.Profissionais.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            // LIKE no SQLite ignora maiúsculas/minúsculas apenas para caracteres ASCII.
            var nome = filtro.Nome.Trim();
            query = query.Where(p => EF.Functions.Like(p.Nome, $"%{nome}%"));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Especialidade))
        {
            var especialidade = filtro.Especialidade.Trim();
            query = query.Where(p => p.Especialidade == especialidade);
        }

        if (filtro.Ativo is { } ativo)
            query = query.Where(p => p.Ativo == ativo);

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.Nome)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProfissionalResponseDto(
                p.Id, p.Nome, p.Telefone, p.Email, p.Especialidade, p.Ativo))
            .ToListAsync(ct);

        return PagedResult<ProfissionalResponseDto>.Criar(items, page, pageSize, totalItems);
    }

    public Task<ProfissionalResponseDto?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        db.Profissionais
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProfissionalResponseDto(
                p.Id, p.Nome, p.Telefone, p.Email, p.Especialidade, p.Ativo))
            .FirstOrDefaultAsync(ct);

    public async Task<ProfissionalResponseDto> CriarAsync(
        ProfissionalCreateDto dto, CancellationToken ct = default)
    {
        var profissional = new Profissional
        {
            Nome = dto.Nome.Trim(),
            Telefone = dto.Telefone,
            Email = Texto.Limpar(dto.Email),
            Especialidade = Texto.Limpar(dto.Especialidade),
            Ativo = true
        };

        db.Profissionais.Add(profissional);
        await db.SaveChangesAsync(ct);

        return ParaDto(profissional);
    }

    public async Task<ProfissionalResponseDto?> AtualizarAsync(
        int id, ProfissionalUpdateDto dto, CancellationToken ct = default)
    {
        var profissional = await db.Profissionais.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (profissional is null)
            return null;

        profissional.Nome = dto.Nome.Trim();
        profissional.Telefone = dto.Telefone;
        profissional.Email = Texto.Limpar(dto.Email);
        profissional.Especialidade = Texto.Limpar(dto.Especialidade);

        await db.SaveChangesAsync(ct);

        return ParaDto(profissional);
    }

    public async Task<ProfissionalResponseDto?> AlterarAtivoAsync(
        int id, bool ativo, CancellationToken ct = default)
    {
        var profissional = await db.Profissionais.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (profissional is null)
            return null;

        // Inativar não mexe em agendamentos já criados (RN29).
        profissional.Ativo = ativo;
        await db.SaveChangesAsync(ct);

        return ParaDto(profissional);
    }

    // ---------- Disponibilidade semanal ----------

    public async Task<IReadOnlyList<FaixaDisponibilidadeResponseDto>?> ObterDisponibilidadeAsync(
        int id, CancellationToken ct = default)
    {
        if (!await db.Profissionais.AnyAsync(p => p.Id == id, ct))
            return null;

        return await ListarFaixasAsync(id, ct);
    }

    public async Task<IReadOnlyList<FaixaDisponibilidadeResponseDto>?> DefinirDisponibilidadeAsync(
        int id, DefinirDisponibilidadeDto dto, CancellationToken ct = default)
    {
        if (!await db.Profissionais.AnyAsync(p => p.Id == id, ct))
            return null;

        // Substitui a grade semanal inteira. Os agendamentos já criados não são alterados (RN29).
        var existentes = await db.Disponibilidades
            .Where(d => d.ProfissionalId == id)
            .ToListAsync(ct);

        db.Disponibilidades.RemoveRange(existentes);

        foreach (var faixa in dto.Faixas ?? Array.Empty<FaixaDisponibilidadeDto>())
        {
            db.Disponibilidades.Add(new DisponibilidadeProfissional
            {
                ProfissionalId = id,
                DiaSemana = faixa.DiaSemana!.Value,
                HoraInicio = faixa.HoraInicio!.Value,
                HoraFim = faixa.HoraFim!.Value
            });
        }

        await db.SaveChangesAsync(ct);

        return await ListarFaixasAsync(id, ct);
    }

    private async Task<IReadOnlyList<FaixaDisponibilidadeResponseDto>> ListarFaixasAsync(
        int profissionalId, CancellationToken ct)
    {
        var faixas = await db.Disponibilidades
            .AsNoTracking()
            .Where(d => d.ProfissionalId == profissionalId)
            .ToListAsync(ct);

        // Ordena em memória (dia da semana e hora inicial)
        return faixas
            .OrderBy(f => f.DiaSemana)
            .ThenBy(f => f.HoraInicio)
            .Select(f => new FaixaDisponibilidadeResponseDto(f.Id, f.DiaSemana, f.HoraInicio, f.HoraFim))
            .ToList();
    }

    // ---------- Serviços executados ----------

    public async Task<IReadOnlyList<ServicoResponseDto>?> ListarServicosAsync(
        int id, CancellationToken ct = default)
    {
        if (!await db.Profissionais.AnyAsync(p => p.Id == id, ct))
            return null;

        return await ListarServicosDoProfissionalAsync(id, ct);
    }

    public async Task<Resultado<IReadOnlyList<ServicoResponseDto>>> DefinirServicosAsync(
        int id, DefinirServicosDto dto, CancellationToken ct = default)
    {
        var profissional = await db.Profissionais
            .Include(p => p.Servicos)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (profissional is null)
            return Resultado<IReadOnlyList<ServicoResponseDto>>.NaoEncontrado($"Profissional {id} não encontrado.");

        var novos = (dto.ServicoIds ?? Array.Empty<int>()).Distinct().ToList();

        var encontrados = await db.Servicos
            .Where(s => novos.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(ct);

        var inexistentes = novos.Except(encontrados).ToList();
        if (inexistentes.Count > 0)
            return Resultado<IReadOnlyList<ServicoResponseDto>>.NaoEncontrado(
                $"Serviço(s) não encontrado(s): {string.Join(", ", inexistentes)}.");

        // Aplica só a diferença (remove os que saíram, adiciona os novos)
        var atuais = profissional.Servicos.Select(ps => ps.ServicoId).ToHashSet();

        var remover = profissional.Servicos.Where(ps => !novos.Contains(ps.ServicoId)).ToList();
        db.ProfissionaisServicos.RemoveRange(remover);

        foreach (var servicoId in novos.Where(s => !atuais.Contains(s)))
            profissional.Servicos.Add(new ProfissionalServico { ServicoId = servicoId });

        await db.SaveChangesAsync(ct);

        return Resultado<IReadOnlyList<ServicoResponseDto>>.Ok(
            await ListarServicosDoProfissionalAsync(id, ct));
    }

    private async Task<IReadOnlyList<ServicoResponseDto>> ListarServicosDoProfissionalAsync(
        int profissionalId, CancellationToken ct) =>
        await db.ProfissionaisServicos
            .AsNoTracking()
            .Where(ps => ps.ProfissionalId == profissionalId)
            .Select(ps => ps.Servico)
            .OrderBy(s => s.Nome)
            .Select(s => new ServicoResponseDto(
                s.Id, s.Nome, s.Descricao, s.Preco, s.DuracaoMinutos, s.Ativo))
            .ToListAsync(ct);

    private static ProfissionalResponseDto ParaDto(Profissional p) =>
        new(p.Id, p.Nome, p.Telefone, p.Email, p.Especialidade, p.Ativo);
}
