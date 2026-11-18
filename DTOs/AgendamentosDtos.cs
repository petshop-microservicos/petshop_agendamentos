using System.ComponentModel.DataAnnotations;
using Agendamentos.Api.Data;

namespace Agendamentos.Api.DTOs;

// Horários em hora local do petshop, sem fuso: 2026-11-20T14:00:00
public record AgendamentoCreateDto(
    [Range(1, int.MaxValue)] int AnimalId,
    [Range(1, int.MaxValue)] int ProfissionalId,
    [Range(1, int.MaxValue)] int ServicoId,
    [Required] DateTime? Inicio) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        ValidacaoAgendamento.HorarioLocal(Inicio);
}

public record RemarcarAgendamentoDto(
    [Required] DateTime? Inicio) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        ValidacaoAgendamento.HorarioLocal(Inicio);
}

// RN07: o funcionário registra o andamento (EmAndamento, Concluido, Cancelado, Faltou).
public record AlterarStatusAgendamentoDto(
    [Required, EnumDataType(typeof(StatusAgendamento))] StatusAgendamento? Status);

public record AgendamentoResponseDto(
    int Id,
    int AnimalId,
    int ProfissionalId,
    string ProfissionalNome,
    int ServicoId,
    string ServicoNome,
    DateTime Inicio,
    DateTime Fim,
    int DuracaoMinutos,
    decimal Preco,
    StatusAgendamento Status,
    DateTime CriadoEm);

public class AgendamentoFiltroDto : PaginacaoDto
{
    public int? AnimalId { get; init; }
    public int? ProfissionalId { get; init; }
    public int? ServicoId { get; init; }
    public StatusAgendamento? Status { get; init; }

    // Filtra pelo início do agendamento: De <= Inicio < Ate
    public DateTime? De { get; init; }
    public DateTime? Ate { get; init; }
}

// Dados do animal lidos do serviço AnimaisTutores (GET /api/animais/{id}).
// Ativo é opcional: o serviço atual considera "ativo" um animal que existe;
// se passar a expor o campo, Ativo = false bloqueia o agendamento (RN04).
public record AnimalExternoDto(int Id, string Nome, bool? Ativo = null);

internal static class ValidacaoAgendamento
{
    // Rejeita horários com fuso ("Z" ou "+03:00") para não converter o horário em silêncio.
    public static IEnumerable<ValidationResult> HorarioLocal(DateTime? inicio)
    {
        if (inicio is not null && inicio.Value.Kind != DateTimeKind.Unspecified)
        {
            yield return new ValidationResult(
                "Informe o horário local, sem fuso horário (ex.: 2026-11-20T14:00:00).",
                ["Inicio"]);
        }
    }
}
