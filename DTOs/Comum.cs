using System.ComponentModel.DataAnnotations;

namespace Agendamentos.Api.DTOs;

public class PaginacaoDto
{
    public const int PageSizeMaximo = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    public (int Page, int PageSize) Normalizado() =>
        (Math.Max(Page, 1), Math.Clamp(PageSize, 1, PageSizeMaximo));
}

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages)
{
    public static PagedResult<T> Criar(IReadOnlyList<T> items, int page, int pageSize, int totalItems) =>
        new(items, page, pageSize, totalItems, (int)Math.Ceiling(totalItems / (double)pageSize));
}

// Corpo de PATCH .../ativo (serviços e profissionais são inativados, nunca excluídos: RN29).
public record AlterarAtivoDto([property: Required] bool? Ativo);

public enum StatusResultado
{
    Sucesso,
    NaoEncontrado,
    Conflito,
    Indisponivel
}

// Resultado de operações que podem falhar por regra de negócio (sem usar exceções).
public record Resultado<T>(StatusResultado Status, T? Valor = default, string? Mensagem = null)
{
    public bool Sucesso => Status == StatusResultado.Sucesso;

    public static Resultado<T> Ok(T valor) => new(StatusResultado.Sucesso, valor);
    public static Resultado<T> NaoEncontrado(string mensagem) => new(StatusResultado.NaoEncontrado, default, mensagem);
    public static Resultado<T> Conflito(string mensagem) => new(StatusResultado.Conflito, default, mensagem);
    public static Resultado<T> Indisponivel(string mensagem) => new(StatusResultado.Indisponivel, default, mensagem);
}

internal static class Texto
{
    public static string? Limpar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
