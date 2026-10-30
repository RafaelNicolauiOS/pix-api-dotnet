using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PixApi.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriaContaELancamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conta",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    titular = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    chave_pix = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    saldo = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conta", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lancamento",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conta_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    realizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    transferencia_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lancamento", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uk_conta_chave_pix",
                table: "conta",
                column: "chave_pix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_lancamento_conta",
                table: "lancamento",
                column: "conta_id");

            migrationBuilder.CreateIndex(
                name: "idx_lancamento_realizado_em",
                table: "lancamento",
                column: "realizado_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conta");

            migrationBuilder.DropTable(
                name: "lancamento");
        }
    }
}
