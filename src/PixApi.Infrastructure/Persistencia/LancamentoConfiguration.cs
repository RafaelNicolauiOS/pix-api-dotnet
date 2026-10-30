using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PixApi.Domain;

namespace PixApi.Infrastructure.Persistencia;

// Sem chave estrangeira para conta: quando uma conta é encerrada, a linha em "conta" some,
// mas o histórico contábil precisa continuar existindo (igual à V2 do Flyway no Java).
public class LancamentoConfiguration : IEntityTypeConfiguration<Lancamento>
{
    public void Configure(EntityTypeBuilder<Lancamento> lancamento)
    {
        lancamento.ToTable("lancamento");
        lancamento.HasKey(l => l.Id);
        lancamento.Property(l => l.Id).HasColumnName("id").ValueGeneratedOnAdd();
        lancamento.Property(l => l.ContaId).HasColumnName("conta_id").IsRequired();
        // Grava "PIX_ENVIADO" (legível), não a posição do enum: o @Enumerated(STRING) do JPA.
        lancamento.Property(l => l.Tipo).HasColumnName("tipo").HasConversion<string>().HasMaxLength(12).IsRequired();
        lancamento.Property(l => l.Valor).HasColumnName("valor").HasPrecision(15, 2).IsRequired();
        lancamento.Property(l => l.RealizadoEm).HasColumnName("realizado_em").IsRequired();
        lancamento.Property(l => l.TransferenciaId).HasColumnName("transferencia_id");
        lancamento.HasIndex(l => l.ContaId).HasDatabaseName("idx_lancamento_conta");
        lancamento.HasIndex(l => l.RealizadoEm).HasDatabaseName("idx_lancamento_realizado_em");
    }
}
