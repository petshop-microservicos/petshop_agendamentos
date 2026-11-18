using System.Linq.Expressions;
using Agendamentos.Api.Data;
using Agendamentos.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using Res = Agendamentos.Api.DTOs.Resultado<Agendamentos.Api.DTOs.AgendamentoResponseDto>;

namespace Agendamentos.Api.Services;

public interface IAgendamentosService
{
    Task<PagedResult<AgendamentoResponseDto>> ListarAsync(AgendamentoFiltroDto filtro, CancellationToken ct = default);
    Task<AgendamentoResponseDto?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<Res> CriarAsync(AgendamentoCreateDto dto, CancellationToken ct = default);
    Task<Res> RemarcarAsync(int id, RemarcarAgendamentoDto dto, CancellationToken ct = default);
    Task<Res> AlterarStatusAsync(int id, AlterarStatusAgendamentoDto dto, CancellationToken ct = default);
}

public class AgendamentosService(AppDbContext db, IAnimaisTutoresClient animais) : IAgendamentosService
{
    private static readonly Expression<Func<Agendamento, AgendamentoResponseDto>> Projecao = a =>
        new AgendamentoResponseDto(
            a.Id,
            a.AnimalId,
            a.ProfissionalId,
            a.Profissional.Nome,
            a.ServicoId,
            a.Servico.Nome,
            a.Inicio,
            a.Fim,
            a.DuracaoMinutos,
            a.Preco,
            a.Status,
            a.CriadoEm);

    // ---------- Consultas ----------

    public async Task<PagedResult<AgendamentoResponseDto>> ListarAsync(
        AgendamentoFiltroDto filtro, CancellationToken ct = default)
    {
        var (page, pageSize) = filtro.Normalizado();

        var query = db.Agendamentos.AsNoTracking().AsQueryable();

        if (filtro.AnimalId is { } animalId)
            query = query.Where(a => a.AnimalId == animalId);

        if (filtro.ProfissionalId is { } profissionalId)
            query = query.Where(a => a.ProfissionalId == profissionalId);

        if (filtro.ServicoId is { } servicoId)
            query = query.Where(a => a.ServicoId == servicoId);

        if (filtro.Status is { } status)
            query = query.Where(a => a.Status == status);

        if (filtro.De is { } de)
            query = query.Where(a => a.Inicio >= de);

        if (filtro.Ate is { } ate)
            query = query.Where(a => a.Inicio < ate);

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .OrderBy(a => a.Inicio)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Projecao)
            .ToListAsync(ct);

        return PagedResult<AgendamentoResponseDto>.Criar(items, page, pageSize, totalItems);
    }

    public Task<AgendamentoResponseDto?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        db.Agendamentos
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(Projecao)
            .FirstOrDefaultAsync(ct);

    // ---------- Criação ----------

    public async Task<Res> CriarAsync(AgendamentoCreateDto dto, CancellationToken ct = default)
    {
        var inicio = dto.Inicio!.Value;

        var servico = await db.Servicos.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == dto.ServicoId, ct);
        if (servico is null)
            return Res.NaoEncontrado($"Serviço {dto.ServicoId} não encontrado.");

        var profissional = await db.Profissionais.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == dto.ProfissionalId, ct);
        if (profissional is null)
            return Res.NaoEncontrado($"Profissional {dto.ProfissionalId} não encontrado.");

        // RN04: serviço e profissional ativos
        if (!servico.Ativo)
            return Res.Conflito("O serviço está inativo.");

        if (!profissional.Ativo)
            return Res.Conflito("O profissional está inativo.");

        // RN26: o profissional só pode ser agendado para os serviços vinculados a ele
        var vinculado = await db.ProfissionaisServicos.AnyAsync(
            ps => ps.ProfissionalId == dto.ProfissionalId && ps.ServicoId == dto.ServicoId, ct);
        if (!vinculado)
            return Res.Conflito("O profissional não executa este serviço.");

        // RN04: animal ativo (consulta o serviço AnimaisTutores)
        var animal = await animais.ObterAnimalAsync(dto.AnimalId, ct);
        if (animal.Status == StatusResultado.NaoEncontrado)
            return Res.NaoEncontrado(animal.Mensagem ?? $"Animal {dto.AnimalId} não encontrado.");

        if (animal.Status == StatusResultado.Indisponivel)
            return Res.Indisponivel(animal.Mensagem ?? "Serviço de animais indisponível.");

        if (animal.Valor!.Ativo == false)
            return Res.Conflito("O animal está inativo.");

        // RN01 e RN05: o intervalo usa a duração do serviço no momento da criação
        var fim = inicio.AddMinutes(servico.DuracaoMinutos);

        // RN03: dentro da disponibilidade semanal do profissional
        if (!await DentroDaDisponibilidadeAsync(dto.ProfissionalId, inicio, fim, ct))
            return Res.Conflito("O horário está fora da disponibilidade do profissional.");

        // RN02: sem sobreposição para o profissional nem para o animal
        var conflito = await BuscarConflitoAsync(dto.ProfissionalId, dto.AnimalId, inicio, fim, null, ct);
        if (conflito is not null)
            return Res.Conflito(conflito);

        // Nota: a checagem e a gravação não são atômicas. Com um único processo e SQLite o risco é baixo;
        // para escala maior, trate a concorrência no banco (transação serializável ou restrição).
        var agendamento = new Agendamento
        {
            AnimalId = dto.AnimalId,
            ProfissionalId = dto.ProfissionalId,
            ServicoId = dto.ServicoId,
            Inicio = inicio,
            Fim = fim,
            DuracaoMinutos = servico.DuracaoMinutos,
            Preco = servico.Preco,
            Status = StatusAgendamento.Agendado,
            CriadoEm = DateTime.UtcNow
        };

        db.Agendamentos.Add(agendamento);
        await db.SaveChangesAsync(ct);

        return Res.Ok(ParaDto(agendamento, profissional.Nome, servico.Nome));
    }

    // ---------- Remarcação ----------

    public async Task<Res> RemarcarAsync(int id, RemarcarAgendamentoDto dto, CancellationToken ct = default)
    {
        var agendamento = await db.Agendamentos
            .Include(a => a.Profissional)
            .Include(a => a.Servico)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (agendamento is null)
            return Res.NaoEncontrado($"Agendamento {id} não encontrado.");

        // RN06
        if (agendamento.Status != StatusAgendamento.Agendado)
            return Res.Conflito("Somente agendamentos com status Agendado podem ser remarcados.");

        var inicio = dto.Inicio!.Value;

        // RN05: a duração guardada no agendamento é mantida
        var fim = inicio.AddMinutes(agendamento.DuracaoMinutos);

        // RN03 (com a disponibilidade atual do profissional) e RN02
        if (!await DentroDaDisponibilidadeAsync(agendamento.ProfissionalId, inicio, fim, ct))
            return Res.Conflito("O horário está fora da disponibilidade do profissional.");

        var conflito = await BuscarConflitoAsync(
            agendamento.ProfissionalId, agendamento.AnimalId, inicio, fim, agendamento.Id, ct);
        if (conflito is not null)
            return Res.Conflito(conflito);

        agendamento.Inicio = inicio;
        agendamento.Fim = fim;

        await db.SaveChangesAsync(ct);

        return Res.Ok(ParaDto(agendamento, agendamento.Profissional.Nome, agendamento.Servico.Nome));
    }

    // ---------- Status (RN06, RN07) ----------

    public async Task<Res> AlterarStatusAsync(
        int id, AlterarStatusAgendamentoDto dto, CancellationToken ct = default)
    {
        var agendamento = await db.Agendamentos
            .Include(a => a.Profissional)
            .Include(a => a.Servico)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (agendamento is null)
            return Res.NaoEncontrado($"Agendamento {id} não encontrado.");

        var atual = agendamento.Status;
        var novo = dto.Status!.Value;

        if (!TransicaoPermitida(atual, novo))
        {
            var mensagem = novo == StatusAgendamento.Cancelado
                ? "Somente agendamentos com status Agendado podem ser cancelados."
                : $"Não é possível alterar o status de {atual} para {novo}.";

            return Res.Conflito(mensagem);
        }

        agendamento.Status = novo;
        await db.SaveChangesAsync(ct);

        return Res.Ok(ParaDto(agendamento, agendamento.Profissional.Nome, agendamento.Servico.Nome));
    }

    // Agendado -> EmAndamento | Cancelado | Faltou
    // EmAndamento -> Concluido
    // Concluido, Cancelado e Faltou são finais.
    private static bool TransicaoPermitida(StatusAgendamento atual, StatusAgendamento novo) =>
        (atual, novo) switch
        {
            (StatusAgendamento.Agendado, StatusAgendamento.EmAndamento) => true,
            (StatusAgendamento.Agendado, StatusAgendamento.Cancelado) => true,
            (StatusAgendamento.Agendado, StatusAgendamento.Faltou) => true,
            (StatusAgendamento.EmAndamento, StatusAgendamento.Concluido) => true,
            _ => false
        };

    // ---------- Regras de horário ----------

    // RN03: [inicio, fim) precisa caber na grade do dia da semana. Faixas encostadas
    // (ex.: 08:00-12:00 e 12:00-18:00) são tratadas como uma faixa contínua.
    private async Task<bool> DentroDaDisponibilidadeAsync(
        int profissionalId, DateTime inicio, DateTime fim, CancellationToken ct)
    {
        // As faixas são por dia: um atendimento que vira a meia-noite não cabe em nenhuma
        if (inicio.Date != fim.Date)
            return false;

        var dia = (DiaSemana)(int)inicio.DayOfWeek;

        var faixas = await db.Disponibilidades
            .AsNoTracking()
            .Where(d => d.ProfissionalId == profissionalId && d.DiaSemana == dia)
            .ToListAsync(ct);

        var horaInicio = TimeOnly.FromDateTime(inicio);
        var horaFim = TimeOnly.FromDateTime(fim);

        TimeOnly? alcance = null;

        foreach (var faixa in faixas.OrderBy(f => f.HoraInicio))
        {
            if (alcance is null)
            {
                if (faixa.HoraInicio <= horaInicio && horaInicio < faixa.HoraFim)
                    alcance = faixa.HoraFim;
            }
            else if (faixa.HoraInicio <= alcance.Value && faixa.HoraFim > alcance.Value)
            {
                alcance = faixa.HoraFim;
            }
        }

        return alcance is not null && horaFim <= alcance.Value;
    }

    // RN02: dois intervalos [a, b) e [c, d) se sobrepõem quando a < d e c < b.
    // Só contam agendamentos ativos (Agendado ou EmAndamento).
    private async Task<string?> BuscarConflitoAsync(
        int profissionalId, int animalId, DateTime inicio, DateTime fim, int? ignorarId, CancellationToken ct)
    {
        var query = db.Agendamentos
            .AsNoTracking()
            .Where(a => (a.Status == StatusAgendamento.Agendado || a.Status == StatusAgendamento.EmAndamento)
                        && a.Inicio < fim
                        && a.Fim > inicio);

        if (ignorarId is { } idIgnorado)
            query = query.Where(a => a.Id != idIgnorado);

        if (await query.AnyAsync(a => a.ProfissionalId == profissionalId, ct))
            return "O profissional já possui um agendamento nesse horário.";

        if (await query.AnyAsync(a => a.AnimalId == animalId, ct))
            return "O animal já possui um agendamento nesse horário.";

        return null;
    }

    private static AgendamentoResponseDto ParaDto(Agendamento a, string profissionalNome, string servicoNome) =>
        new(a.Id, a.AnimalId, a.ProfissionalId, profissionalNome, a.ServicoId, servicoNome,
            a.Inicio, a.Fim, a.DuracaoMinutos, a.Preco, a.Status, a.CriadoEm);
}
