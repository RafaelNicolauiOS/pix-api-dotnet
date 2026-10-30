using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.OpenApi.Models;
using PixApi.Api.Configuracao;
using PixApi.Api.Erros;
using PixApi.Application.Batch;
using PixApi.Application.Contas;
using PixApi.Application.Transferencias;
using PixApi.Infrastructure;
using PixApi.Infrastructure.Mensageria;
using PixApi.Infrastructure.Monitoramento;

// Program.cs = ponto de entrada (o PixApiApplication.main + as classes @Configuration do Spring).
// Fase 1: registrar serviços no container de DI. Fase 2: montar o pipeline de middlewares.
var builder = WebApplication.CreateBuilder(args);

// O Render informa a porta na variável PORT (localmente: 8080, padrão das imagens .NET 8).
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } porta)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{porta}");
}

// Logs: além do console, um buffer em memória com as últimas 500 linhas (GET /logs).
var logsEmMemoria = new LogsEmMemoria(500);
builder.Logging.AddProvider(logsEmMemoria);
builder.Services.AddSingleton(logsEmMemoria);

// Casos de uso (Application) e adaptadores (Infrastructure).
builder.Services.AddSingleton(TimeProvider.System); // relógio injetável: nos testes, um FakeTimeProvider
builder.Services.AddScoped<ContaService>();
builder.Services.AddScoped<TransferenciaService>();
builder.Services.AddScoped<ExportacaoMovimentoService>();
builder.Services.AdicionarInfraestrutura();
builder.Services.AddHostedService<DadosIniciais>(); // depois da InicializacaoDoBanco (ordem de registro)

builder.Services
    .AddControllers(opcoes =>
    {
        opcoes.Filters.Add<ApiExceptionFilter>();
        // Erros de validação usam o nome do campo no JSON ("chavePix"), e não o da propriedade C# ("ChavePix").
        opcoes.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
    })
    .AddJsonOptions(opcoes => JsonPix.Configurar(opcoes.JsonSerializerOptions))
    .ConfigureApiBehaviorOptions(opcoes => opcoes.InvalidModelStateResponseFactory = RespostaDeValidacao.Criar);

builder.Services.AddExceptionHandler<ErroInesperadoHandler>();
builder.Services.AddProblemDetails();
builder.Services.AdicionarCors(builder.Configuration);

// Swagger/OpenAPI: documento em /swagger/v1/swagger.json e interface em /swagger.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opcoes =>
{
    opcoes.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "pix-api (.NET)",
        Version = "v1",
        Description = "Contas e transferências Pix. Mesmo contrato do pix-api em Java/Spring Boot.",
    });
    opcoes.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
});

var app = builder.Build();

// Pipeline: cada middleware embrulha o próximo (como uma cadeia de Filters do Servlet).
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(Cors.Politica);
app.MapControllers();

app.Run();

// Torna a classe Program visível para o WebApplicationFactory<Program> dos testes.
public partial class Program;
