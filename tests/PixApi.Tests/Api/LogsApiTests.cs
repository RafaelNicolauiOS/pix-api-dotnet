using System.Net;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Api;

public class LogsComTokenFactory : PixApiFactory
{
    protected override Dictionary<string, string?> Configuracoes => new() { ["Pix:Admin:Token"] = "segredo-de-teste" };
}

// Equivale ao LogsControllerTest com @Nested do Java: duas fábricas, duas configurações.
public class LogsApiTests
{
    public class ComTokenConfigurado(LogsComTokenFactory api) : IClassFixture<LogsComTokenFactory>
    {
        private readonly HttpClient _http = api.CreateClient();

        private Task<HttpResponseMessage> Logs(string url, string? token)
        {
            var requisicao = new HttpRequestMessage(HttpMethod.Get, url);
            if (token is not null)
            {
                requisicao.Headers.Add("X-Admin-Token", token);
            }
            return _http.SendAsync(requisicao);
        }

        [Fact]
        public async Task SemToken_DeveRetornar401()
        {
            var resposta = await Logs("/logs", null);

            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
            Assert.Equal("application/problem+json", resposta.TipoDeConteudo());
            Assert.Equal("Token inválido", (string)(await resposta.Json())["title"]!);
        }

        [Fact]
        public async Task ComTokenErrado_DeveRetornar401()
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Logs("/logs", "chute")).StatusCode);
        }

        [Fact]
        public async Task ComTokenCorreto_DeveRetornarAsLinhasCapturadas()
        {
            await _http.GetAsync("/contas/424242"); // gera ao menos uma linha de log (a requisição)

            var resposta = await Logs("/logs?limite=500", "segredo-de-teste");

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var linhas = (await resposta.Json()).AsArray();
            Assert.NotEmpty(linhas);
            var linha = linhas[^1]!;
            Assert.NotNull(linha["instante"]);
            Assert.Contains((string)linha["nivel"]!, new[] { "TRACE", "DEBUG", "INFO", "WARN", "ERROR" });
            Assert.DoesNotContain(".", (string)linha["logger"]!); // só o nome curto da classe
            Assert.NotNull(linha["mensagem"]);
        }

        [Fact]
        public async Task DeveRespeitarOLimite()
        {
            var linhas = (await (await Logs("/logs?limite=1", "segredo-de-teste")).Json()).AsArray();

            Assert.Single(linhas);
        }
    }

    public class SemTokenConfigurado(PixApiFactory api) : IClassFixture<PixApiFactory>
    {
        [Fact]
        public async Task EndpointDeveFicarDesativado()
        {
            var requisicao = new HttpRequestMessage(HttpMethod.Get, "/logs");
            requisicao.Headers.Add("X-Admin-Token", "");

            var resposta = await api.CreateClient().SendAsync(requisicao);

            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
            Assert.Equal("Logs desativados", (string)(await resposta.Json())["title"]!);
        }
    }
}
