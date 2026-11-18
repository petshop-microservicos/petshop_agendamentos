namespace Agendamentos.Api.Data;

public class Servico
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string? Descricao { get; set; }
    public decimal Preco { get; set; }
    public int DuracaoMinutos { get; set; }
    public bool Ativo { get; set; } = true;

    public List<ProfissionalServico> Profissionais { get; set; } = new();
}
