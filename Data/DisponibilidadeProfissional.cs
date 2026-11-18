namespace Agendamentos.Api.Data;

// Uma faixa da grade semanal recorrente do profissional (RN27).
public class DisponibilidadeProfissional
{
    public int Id { get; set; }

    public int ProfissionalId { get; set; }
    public Profissional Profissional { get; set; } = null!;

    public DiaSemana DiaSemana { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFim { get; set; }
}
