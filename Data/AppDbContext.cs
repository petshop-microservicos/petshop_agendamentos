using Microsoft.EntityFrameworkCore;

namespace Agendamentos.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<Profissional> Profissionais => Set<Profissional>();
    public DbSet<DisponibilidadeProfissional> Disponibilidades => Set<DisponibilidadeProfissional>();
    public DbSet<ProfissionalServico> ProfissionaisServicos => Set<ProfissionalServico>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Servico>(e =>
        {
            // NOCASE: "Banho" e "banho" contam como o mesmo nome no índice único
            e.Property(s => s.Nome).HasMaxLength(100).UseCollation("NOCASE");
            e.Property(s => s.Descricao).HasMaxLength(300);
            e.Property(s => s.Preco).HasPrecision(10, 2);
            e.HasIndex(s => s.Nome).IsUnique();
        });

        modelBuilder.Entity<Profissional>(e =>
        {
            e.Property(p => p.Nome).HasMaxLength(150);
            e.Property(p => p.Telefone).HasMaxLength(11);
            e.Property(p => p.Email).HasMaxLength(150);
            e.Property(p => p.Especialidade).HasMaxLength(80);
        });

        modelBuilder.Entity<DisponibilidadeProfissional>(e =>
        {
            e.HasOne(d => d.Profissional)
             .WithMany(p => p.Disponibilidades)
             .HasForeignKey(d => d.ProfissionalId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(d => new { d.ProfissionalId, d.DiaSemana });
        });

        modelBuilder.Entity<ProfissionalServico>(e =>
        {
            e.HasKey(ps => new { ps.ProfissionalId, ps.ServicoId });

            e.HasOne(ps => ps.Profissional)
             .WithMany(p => p.Servicos)
             .HasForeignKey(ps => ps.ProfissionalId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ps => ps.Servico)
             .WithMany(s => s.Profissionais)
             .HasForeignKey(ps => ps.ServicoId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Agendamento>(e =>
        {
            e.Property(a => a.Preco).HasPrecision(10, 2);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

            // Serviço e profissional nunca são excluídos, só inativados (RN29)
            e.HasOne(a => a.Profissional)
             .WithMany()
             .HasForeignKey(a => a.ProfissionalId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Servico)
             .WithMany()
             .HasForeignKey(a => a.ServicoId)
             .OnDelete(DeleteBehavior.Restrict);

            // Apoiam as consultas de sobreposição (RN02) e as listagens
            e.HasIndex(a => new { a.ProfissionalId, a.Inicio });
            e.HasIndex(a => new { a.AnimalId, a.Inicio });
            e.HasIndex(a => a.Status);
        });
    }
}
