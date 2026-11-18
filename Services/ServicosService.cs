using Agendamentos.Api.Data;
using Agendamentos.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Agendamentos.Api.Services;

public interface IServicosService
{
    Task<PagedResult<ServicoResponseDto>> ListarAsync(ServicoFiltroDto filtro, CancellationToken ct = default);
    Task<ServicoResponseDto?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<Resultado<ServicoResponseDto>> CriarAsync(ServicoCreateDto dto, CancellationToken ct = default);
    Task<Resultado<ServicoResponseDto>> AtualizarAsync(int id, ServicoUpdateDto dto, CancellationToken ct = default);

    // Inativa ou reativa. Retorna null se o serviço não existir.
    Task<ServicoResponseDto?> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default);
}

public class ServicosService(AppDbContext db) : IServicosService
{
    private const string MensagemNomeDuplicado = "Já existe um serviço cadastrado com este nome.";

    public async Task<PagedResult<ServicoResponseDto>> ListarAsync(
        ServicoFiltroDto filtro, CancellationToken ct = default)
    {
        var (page, pageSize) = filtro.Normalizado();

        var query = db.Servicos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            // LIKE no SQLite ignora maiúsculas/minúsculas apenas para caracteres ASCII.
            var nome = filtro.Nome.Trim();
            query = query.Where(s => EF.Functions.Like(s.Nome, $"%{nome}%"));
        }

        if (filtro.Ativo is { } ativo)
            query = query.Where(s => s.Ativo == ativo);

        var totalItems = await query.CountAsync(ct);

        // Ordena só por Nome/Id: o SQLite não ordena colunas decimal (Preco) no banco.
        var items = await query
            .OrderBy(s => s.Nome)
            .ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ServicoResponseDto(
                s.Id, s.Nome, s.Descricao, s.Preco, s.DuracaoMinutos, s.Ativo))
            .ToListAsync(ct);

        return PagedResult<ServicoResponseDto>.Criar(items, page, pageSize, totalItems);
    }

    public Task<ServicoResponseDto?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        db.Servicos
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new ServicoResponseDto(
                s.Id, s.Nome, s.Descricao, s.Preco, s.DuracaoMinutos, s.Ativo))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<ServicoResponseDto>> CriarAsync(
        ServicoCreateDto dto, CancellationToken ct = default)
    {
        var nome = dto.Nome.Trim();

        if (await db.Servicos.AnyAsync(s => s.Nome == nome, ct))
            return Resultado<ServicoResponseDto>.Conflito(MensagemNomeDuplicado);

        var servico = new Servico
        {
            Nome = nome,
            Descricao = Texto.Limpar(dto.Descricao),
            Preco = dto.Preco!.Value,
            DuracaoMinutos = dto.DuracaoMinutos!.Value,
            Ativo = true
        };

        db.Servicos.Add(servico);
        await db.SaveChangesAsync(ct);

        return Resultado<ServicoResponseDto>.Ok(ParaDto(servico));
    }

    public async Task<Resultado<ServicoResponseDto>> AtualizarAsync(
        int id, ServicoUpdateDto dto, CancellationToken ct = default)
    {
        var servico = await db.Servicos.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (servico is null)
            return Resultado<ServicoResponseDto>.NaoEncontrado($"Serviço {id} não encontrado.");

        var nome = dto.Nome.Trim();

        if (await db.Servicos.AnyAsync(s => s.Nome == nome && s.Id != id, ct))
            return Resultado<ServicoResponseDto>.Conflito(MensagemNomeDuplicado);

        // Agendamentos já criados guardam o próprio preço e duração (RN05): não são afetados.
        servico.Nome = nome;
        servico.Descricao = Texto.Limpar(dto.Descricao);
        servico.Preco = dto.Preco!.Value;
        servico.DuracaoMinutos = dto.DuracaoMinutos!.Value;

        await db.SaveChangesAsync(ct);

        return Resultado<ServicoResponseDto>.Ok(ParaDto(servico));
    }

    public async Task<ServicoResponseDto?> AlterarAtivoAsync(
        int id, bool ativo, CancellationToken ct = default)
    {
        var servico = await db.Servicos.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (servico is null)
            return null;

        // Inativar não mexe em agendamentos já criados (RN29).
        servico.Ativo = ativo;
        await db.SaveChangesAsync(ct);

        return ParaDto(servico);
    }

    private static ServicoResponseDto ParaDto(Servico s) =>
        new(s.Id, s.Nome, s.Descricao, s.Preco, s.DuracaoMinutos, s.Ativo);
}
