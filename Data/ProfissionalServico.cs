namespace Agendamentos.Api.Data;

// Vínculo N:N entre profissional e os serviços que ele executa (RF10, RN26).
public class ProfissionalServico
{
    public int ProfissionalId { get; set; }
    public Profissional Profissional { get; set; } = null!;

    public int ServicoId { get; set; }
    public Servico Servico { get; set; } = null!;
}
