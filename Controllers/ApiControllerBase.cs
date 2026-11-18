using Agendamentos.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Agendamentos.Api.Controllers;

// Traduz o resultado dos Services em ProblemDetails. Não há exceções de domínio nem handlers globais.
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult Erro(StatusResultado status, string? mensagem) => status switch
    {
        StatusResultado.NaoEncontrado => Problem(
            title: "Não encontrado",
            detail: mensagem,
            statusCode: StatusCodes.Status404NotFound),
        StatusResultado.Conflito => Problem(
            title: "Conflito",
            detail: mensagem,
            statusCode: StatusCodes.Status409Conflict),
        StatusResultado.Indisponivel => Problem(
            title: "Serviço indisponível",
            detail: mensagem,
            statusCode: StatusCodes.Status503ServiceUnavailable),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
}
