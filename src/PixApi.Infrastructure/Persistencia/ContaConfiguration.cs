using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PixApi.Domain;

namespace PixApi.Infrastructure.Persistencia;

// Mapeamento classe ↔ tabela. No Java isso eram anotações (@Entity, @Column) dentro da entidade;
// aqui fica fora do domínio ("Fluent API"), e a Conta não sabe que existe banco.
// Nomes iguais ao schema do Flyway do pix-api (conta, chave_pix, NUMERIC(15,2)...).
public class ContaConfiguration : IEntityTypeConfiguration<Conta>
{
    public void Configure(EntityTypeBuilder<Conta> conta)
    {
        conta.ToTable("conta");
        conta.HasKey(c => c.Id);
        conta.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd(); // IDENTITY no PostgreSQL
        conta.Property(c => c.Titular).HasColumnName("titular").HasMaxLength(255).IsRequired();
        conta.Property(c => c.Cpf).HasColumnName("cpf").HasMaxLength(11).IsRequired();
        conta.Property(c => c.ChavePix).HasColumnName("chave_pix").HasMaxLength(255).IsRequired();
        conta.Property(c => c.Saldo).HasColumnName("saldo").HasPrecision(15, 2).IsRequired();
        conta.HasIndex(c => c.ChavePix).IsUnique().HasDatabaseName("uk_conta_chave_pix");
    }
}
