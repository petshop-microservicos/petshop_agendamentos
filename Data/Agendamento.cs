namespace Agendamentos.Api.Data;

public class Agendamento
{
    public int Id { get; set; }

    // Animal pertence ao serviço AnimaisTutores: guarda-se só o id (sem FK entre bancos).
    public int AnimalId { get; set; }

    public int ProfissionalId { get; set; }
    public Profissional Profissional { get; set; } = null!;

    public int ServicoId { get; set; }
    public Servico Servico { get; set; } = null!;

    // Intervalo [Inicio, Fim) em horário local do petshop, sem fuso (RN01).
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }

    // Copiados do serviço no momento da criação; mudanças posteriores no serviço não os alteram (RN05).
    public int DuracaoMinutos { get; set; }
    public decimal Preco { get; set; }

    public StatusAgendamento Status { get; set; } = StatusAgendamento.Agendado;

    public DateTime CriadoEm { get; set; }
}
