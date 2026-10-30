using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PixApi.Infrastructure.Persistencia;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Integracao;

// Cria um banco descartável no PostgreSQL apontado por PIX_TESTE_POSTGRES e aplica as migrations.
public sealed class PostgresFactory : PixApiFactory
{
    private readonly string _servidor = Environment.GetEnvironmentVariable(PostgresFactAttribute.Variavel) ?? "";
    private readonly string _banco = $"pix_teste_{Guid.NewGuid():N}";

    private string Conexao => new NpgsqlConnectionStringBuilder(_servidor) { Database = _banco }.ConnectionString;

    protected override Dictionary<string, string?> Configuracoes => string.IsNullOrEmpty(_servidor) ? [] : new()
    {
        ["Pix:Banco:Provedor"] = "postgres",
        ["ConnectionStrings:Pix"] = Conexao,
    };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (string.IsNullOrEmpty(_servidor)) return;
        NpgsqlConnection.ClearAllPools();
        using var conexao = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_servidor) { Database = "postgres" }.ConnectionString);
        conexao.Open();
        using var comando = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_banco} WITH (FORCE)", conexao);
        comando.ExecuteNonQuery();
    }
}

// Os testes que o SQLite não consegue provar: migrations do Npgsql e o SELECT ... FOR UPDATE sob concorrência.
public class PostgresTests(PostgresFactory api) : IClassFixture<PostgresFactory>
{
    [PostgresFact]
    public async Task Migrations_AplicadasNaSubida_NenhumaPendente()
    {
        api.CreateClient(); // sobe a aplicação (que migra o banco)
        using var escopo = api.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<PixDbContext>();

        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [PostgresFact]
    public async Task TransferenciasConcorrentes_NaoCriamNemPerdemDinheiro()
    {
        var http = api.CreateClient();
        var chaves = Enumerable.Range(0, 3).Select(_ => HttpExtensions.ChaveUnica("conc")).ToArray();
        foreach (var chave in chaves) await http.CriarConta(chave, "100.00");

        // 300 transferências, 30 por vez, em todos os sentidos (A→B e B→A ao mesmo tempo: o cenário de deadlock).
        var aleatorio = new Random(2026);
        var pedidos = Enumerable.Range(0, 300).Select(_ =>
        {
            var origem = aleatorio.Next(chaves.Length);
            var destino = (origem + 1 + aleatorio.Next(chaves.Length - 1)) % chaves.Length;
            return (Origem: chaves[origem], Destino: chaves[destino], Valor: aleatorio.Next(1, 2000) / 100m);
        }).ToList();

        var confirmadas = new System.Collections.Concurrent.ConcurrentBag<(string Origem, string Destino, decimal Valor)>();
        var status = new System.Collections.Concurrent.ConcurrentBag<HttpStatusCode>();
        await Parallel.ForEachAsync(pedidos, new ParallelOptions { MaxDegreeOfParallelism = 30 }, async (p, _) =>
        {
            var resposta = await http.PostJson("/transferencias",
                $$"""{"chaveOrigem": "{{p.Origem}}", "chaveDestino": "{{p.Destino}}", "valor": {{p.Valor}}}""");
            status.Add(resposta.StatusCode);
            if (resposta.StatusCode == HttpStatusCode.OK) confirmadas.Add(p);
        });

        Assert.All(status, s => Assert.Contains(s, new[] { HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity }));
        using var escopo = api.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<PixDbContext>();
        foreach (var chave in chaves)
        {
            var esperado = 100m - confirmadas.Where(c => c.Origem == chave).Sum(c => c.Valor)
                                + confirmadas.Where(c => c.Destino == chave).Sum(c => c.Valor);
            var conta = await db.Contas.SingleAsync(c => c.ChavePix == chave);
            var razao = (await db.Lancamentos.Where(l => l.ContaId == conta.Id).ToListAsync()).Sum(l => l.ValorComSinal());
            Assert.Equal(esperado, conta.Saldo);
            Assert.Equal(conta.Saldo, razao);
            Assert.True(conta.Saldo >= 0);
        }
    }
}
