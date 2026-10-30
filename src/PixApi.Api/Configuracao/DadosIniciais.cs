using PixApi.Application.Contas;
using PixApi.Application.Portas;
using PixApi.Domain;

namespace PixApi.Api.Configuracao;

// Carrega as contas de exemplo na subida, só se o banco estiver vazio (o CommandLineRunner do Java).
// Passa pelo ContaService para gerar o lançamento de abertura no razão. Desliga com Pix:Seed:Habilitado=false.
public sealed class DadosIniciais(IServiceProvider servicos, IConfiguration config, ILogger<DadosIniciais> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!config.GetValue("Pix:Seed:Habilitado", true) || !config.GetValue("PIX_SEED_ENABLED", true))
        {
            return;
        }
        using var escopo = servicos.CreateScope();
        if (await escopo.ServiceProvider.GetRequiredService<IContaRepositorio>().ContarAsync(ct) > 0)
        {
            return;
        }
        var contas = escopo.ServiceProvider.GetRequiredService<ContaService>();
        await contas.CriarAsync(new Conta("Ana Souza", "11111111111", "ana@pix.com", 1000.00m), ct);
        await contas.CriarAsync(new Conta("Bruno Lima", "22222222222", "bruno@pix.com", 250.00m), ct);
        await contas.CriarAsync(new Conta("Carla Dias", "33333333333", "+5511999990000", 50.00m), ct);
        log.LogInformation("Contas de exemplo carregadas: ana@pix.com, bruno@pix.com, +5511999990000");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
