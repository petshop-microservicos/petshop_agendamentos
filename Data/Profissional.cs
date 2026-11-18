namespace Agendamentos.Api.Data;

public class Profissional
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Telefone { get; set; } = null!;
    public string? Email { get; set; }
    public string? Especialidade { get; set; }
    public bool Ativo { get; set; } = true;

    public List<DisponibilidadeProfissional> Disponibilidades { get; set; } = new();
    public List<ProfissionalServico> Servicos { get; set; } = new();
}
