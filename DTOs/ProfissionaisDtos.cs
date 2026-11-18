using System.ComponentModel.DataAnnotations;
using Agendamentos.Api.Data;

namespace Agendamentos.Api.DTOs;

// ---------- Profissional (RF08) ----------

public record ProfissionalCreateDto(
    [Required, StringLength(150, MinimumLength = 2)] string Nome,
    [Required, RegularExpression(@"^[0-9]{10,11}\z", ErrorMessage = "O telefone deve ter 10 ou 11 dígitos, somente números.")] string Telefone,
    [EmailAddress, StringLength(150)] string? Email,
    [StringLength(80)] string? Especialidade);

public record ProfissionalUpdateDto(
    [Required, StringLength(150, MinimumLength = 2)] string Nome,
    [Required, RegularExpression(@"^[0-9]{10,11}\z", ErrorMessage = "O telefone deve ter 10 ou 11 dígitos, somente números.")] string Telefone,
    [EmailAddress, StringLength(150)] string? Email,
    [StringLength(80)] string? Especialidade);

public record ProfissionalResponseDto(
    int Id,
    string Nome,
    string Telefone,
    string? Email,
    string? Especialidade,
    bool Ativo);

public class ProfissionalFiltroDto : PaginacaoDto
{
    public string? Nome { get; init; }
    public string? Especialidade { get; init; }
    public bool? Ativo { get; init; }
}

// ---------- Disponibilidade semanal (RF09, RN27) ----------

public record FaixaDisponibilidadeDto(
    [Required, EnumDataType(typeof(DiaSemana))] DiaSemana? DiaSemana,
    [Required] TimeOnly? HoraInicio,
    [Required] TimeOnly? HoraFim);

// Substitui a grade semanal inteira do profissional. Lista vazia = sem disponibilidade.
public record DefinirDisponibilidadeDto(
    [Required] IReadOnlyList<FaixaDisponibilidadeDto>? Faixas) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        var completas = (Faixas ?? Array.Empty<FaixaDisponibilidadeDto>())
            .Where(f => f is { DiaSemana: not null, HoraInicio: not null, HoraFim: not null })
            .ToList();

        // RN27: hora final maior que a inicial
        foreach (var f in completas.Where(f => f.HoraFim <= f.HoraInicio))
        {
            yield return new ValidationResult(
                $"Faixa de {f.DiaSemana}: a hora final deve ser maior que a hora inicial.",
                ["Faixas"]);
        }

        // RN27: faixas do mesmo dia não se sobrepõem (faixas encostadas, como 08-12 e 12-18, são válidas)
        foreach (var dia in completas.Where(f => f.HoraFim > f.HoraInicio).GroupBy(f => f.DiaSemana))
        {
            var ordenadas = dia.OrderBy(f => f.HoraInicio).ToList();

            for (var i = 1; i < ordenadas.Count; i++)
            {
                if (ordenadas[i].HoraInicio < ordenadas[i - 1].HoraFim)
                {
                    yield return new ValidationResult(
                        $"As faixas de {dia.Key} não podem se sobrepor.",
                        ["Faixas"]);
                    break;
                }
            }
        }
    }
}

public record FaixaDisponibilidadeResponseDto(
    int Id,
    DiaSemana DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFim);

// ---------- Serviços executados pelo profissional (RF10, RN26) ----------

// Substitui o conjunto de serviços vinculados ao profissional.
public record DefinirServicosDto(
    [Required] IReadOnlyList<int>? ServicoIds);
