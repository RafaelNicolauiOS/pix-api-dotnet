using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace PixApi.Tests.Suporte;

// Sobe a API inteira em memória (o @SpringBootTest + MockMvc do Java): controllers, filtros, DI,
// EF Core e banco de verdade. As requisições passam por um HttpClient sem abrir porta.
//
// Banco: SQLite em memória, um por fábrica. "Mode=Memory;Cache=Shared" + uma conexão aberta
// durante toda a vida da fábrica mantém o banco vivo entre as requisições.
public class PixApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _manterVivo;
    private readonly string _conexao;

    public PixApiFactory()
    {
        _conexao = $"Data Source=pix-testes-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _manterVivo = new SqliteConnection(_conexao);
        _manterVivo.Open();
    }

    // Configurações extras de cada classe de teste (ex.: token do /logs).
    protected virtual Dictionary<string, string?> Configuracoes => [];

    // Troca de serviços por classe de teste (ex.: um publicador espião).
    protected virtual void TrocarServicos(IServiceCollection servicos)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testes");
        // UseSetting entra na configuração antes de o Program.cs ler builder.Configuration.
        builder.UseSetting("Pix:Banco:Provedor", "sqlite");
        builder.UseSetting("ConnectionStrings:Pix", _conexao);
        builder.UseSetting("Pix:Mensageria", "log");
        builder.UseSetting("Pix:Admin:Token", "");
        foreach (var (chave, valor) in Configuracoes)
        {
            builder.UseSetting(chave, valor);
        }
        builder.ConfigureTestServices(TrocarServicos);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _manterVivo.Dispose();
    }
}
