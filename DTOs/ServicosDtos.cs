using System.ComponentModel.DataAnnotations;

namespace Agendamentos.Api.DTOs;

// RN23: preço >= 0 e duração > 0, ambos obrigatórios.
public record ServicoCreateDto(
    [param: Required, StringLength(100, MinimumLength = 2)]
    string Nome,

    [param: StringLength(300)]
    string? Descricao,

    [param: Required(ErrorMessage = "O preço é obrigatório.")]
    [param: Range(0.0, 100000.0, ErrorMessage = "O preço deve estar entre 0 e 100000.")]
    decimal? Preco,

    [param: Required(ErrorMessage = "A duração é obrigatória.")]
    [param: Range(1, 1440, ErrorMessage = "A duração deve estar entre 1 e 1440 minutos.")]
    int? DuracaoMinutos
) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        ValidacaoServico.CasasDecimaisDoPreco(Preco);
}

public record ServicoUpdateDto(
    [param: Required, StringLength(100, MinimumLength = 2)] string Nome,
    [param: StringLength(300)] string? Descricao,
    [param: Required(ErrorMessage = "O preço é obrigatório."), Range(0.0, 100000.0, ErrorMessage = "O preço deve estar entre 0 e 100000.")] decimal? Preco,
    [param: Required(ErrorMessage = "A duração é obrigatória."), Range(1, 1440, ErrorMessage = "A duração deve estar entre 1 e 1440 minutos.")] int? DuracaoMinutos) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        ValidacaoServico.CasasDecimaisDoPreco(Preco);
}

public record ServicoResponseDto(
    int Id,
    string Nome,
    string? Descricao,
    decimal Preco,
    int DuracaoMinutos,
    bool Ativo);

public class ServicoFiltroDto : PaginacaoDto
{
    public string? Nome { get; init; }
    public bool? Ativo { get; init; }
}

internal static class ValidacaoServico
{
    public static IEnumerable<ValidationResult> CasasDecimaisDoPreco(decimal? preco)
    {
        if (preco is not null && decimal.Round(preco.Value, 2) != preco.Value)
        {
            yield return new ValidationResult(
                "O preço deve ter no máximo 2 casas decimais.",
                ["Preco"]);
        }
    }
}
