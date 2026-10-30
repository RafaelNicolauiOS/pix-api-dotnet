using System.Net;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Api;

public class SwaggerTests(PixApiFactory api) : IClassFixture<PixApiFactory>
{
    [Fact]
    public async Task DocumentoOpenApi_ListaTodasAsRotas()
    {
        var resposta = await api.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var caminhos = (await resposta.Json())["paths"]!.AsObject().Select(p => p.Key).ToList();
        Assert.Equivalent(new[] { "/contas", "/contas/{id}", "/transferencias", "/eventos", "/logs", "/batch/movimento" },
            caminhos);
    }
}
