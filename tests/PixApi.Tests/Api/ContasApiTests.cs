using System.Net;
using PixApi.Tests.Suporte;

namespace PixApi.Tests.Api;

// Mesmo contrato do ContaControllerTest do Java, mas de ponta a ponta (HTTP → banco).
// IClassFixture: uma API (e um banco) para todos os testes desta classe.
public class ContasApiTests(PixApiFactory api) : IClassFixture<PixApiFactory>
{
    private readonly HttpClient _http = api.CreateClient();

    [Fact]
    public async Task Post_DeveCriarConta_ERetornar201ComLocation()
    {
        var chave = HttpExtensions.ChaveUnica();

        var resposta = await _http.PostJson("/contas",
            $$"""{"titular": "Rafael Nicolau", "cpf": "12345678901", "chavePix": "{{chave}}", "saldo": 100.00}""");

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await resposta.Json();
        var id = (long)corpo["id"]!;
        Assert.Equal($"http://localhost/contas/{id}", resposta.Headers.Location!.ToString());
        Assert.Equal("Rafael Nicolau", (string)corpo["titular"]!);
        Assert.Equal("12345678901", (string)corpo["cpf"]!);
        Assert.Equal(chave, (string)corpo["chavePix"]!);
        Assert.Equal(100.00m, (decimal)corpo["saldo"]!);
    }

    [Fact]
    public async Task Post_DadosInvalidos_DeveRetornar400ProblemDetails_ComErrosPorCampo()
    {
        var resposta = await _http.PostJson("/contas", """{"titular": "", "cpf": "123", "chavePix": "", "saldo": -5}""");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.TipoDeConteudo());
        var corpo = await resposta.Json();
        Assert.Equal("Bad Request", (string)corpo["title"]!);
        Assert.Equal(400, (int)corpo["status"]!);
        Assert.Equal("Dados inválidos", (string)corpo["detail"]!);
        Assert.Equal("/contas", (string)corpo["instance"]!);
        Assert.Equal("não deve estar em branco", (string)corpo["erros"]!["titular"]!);
        Assert.Equal("deve conter 11 dígitos", (string)corpo["erros"]!["cpf"]!);
        Assert.Equal("deve ser maior ou igual a 0", (string)corpo["erros"]!["saldo"]!);
        Assert.NotNull(corpo["erros"]!["chavePix"]);
    }

    [Fact]
    public async Task Post_SemSaldo_DeveRetornar400()
    {
        // decimal não aceita null: sem "decimal?" no DTO, o saldo ausente viraria 0 sem erro nenhum.
        var resposta = await _http.PostJson("/contas", """{"titular": "A", "cpf": "12345678901", "chavePix": "x@pix.com"}""");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.NotNull((await resposta.Json())["erros"]!["saldo"]);
    }

    [Fact]
    public async Task Post_JsonMalformado_DeveRetornar400ProblemDetails()
    {
        var resposta = await _http.PostJson("/contas", """{"titular": """);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.TipoDeConteudo());
    }

    [Fact]
    public async Task Post_ChaveDuplicada_DeveRetornar409()
    {
        var chave = HttpExtensions.ChaveUnica();
        await _http.CriarConta(chave, "1");

        var resposta = await _http.PostJson("/contas",
            $$"""{"titular": "Outro", "cpf": "12345678901", "chavePix": "{{chave}}", "saldo": 1}""");

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        var corpo = await resposta.Json();
        Assert.Equal("Conflict", (string)corpo["title"]!);
        Assert.Equal($"Chave Pix já cadastrada: {chave}", (string)corpo["detail"]!);
    }

    [Fact]
    public async Task Get_DeveListarContasOrdenadasPorId_IncluindoAsDeExemplo()
    {
        await _http.CriarConta(HttpExtensions.ChaveUnica(), "1");

        var contas = (await (await _http.GetAsync("/contas")).Json()).AsArray();

        var ids = contas.Select(c => (long)c!["id"]!).ToList();
        Assert.Equal(ids.Order(), ids);
        Assert.Contains(contas, c => (string)c!["chavePix"]! == "ana@pix.com" && (decimal)c["saldo"]! >= 0);
        Assert.Contains(contas, c => (string)c!["chavePix"]! == "+5511999990000");
    }

    [Fact]
    public async Task GetPorId_Inexistente_DeveRetornar404ProblemDetails()
    {
        var resposta = await _http.GetAsync("/contas/999999");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.TipoDeConteudo());
        var corpo = await resposta.Json();
        Assert.Equal("Not Found", (string)corpo["title"]!);
        Assert.Equal("Conta não encontrada: id=999999", (string)corpo["detail"]!);
        Assert.Equal("/contas/999999", (string)corpo["instance"]!);
    }

    [Fact]
    public async Task Put_DeveAtualizarTitularEChave_MasNaoOSaldo()
    {
        var chave = HttpExtensions.ChaveUnica();
        var criada = await (await _http.PostJson("/contas",
            $$"""{"titular": "Antes", "cpf": "12345678901", "chavePix": "{{chave}}", "saldo": 10}""")).Json();
        var id = (long)criada["id"]!;
        var novaChave = HttpExtensions.ChaveUnica();

        var resposta = await _http.PutJson($"/contas/{id}",
            $$"""{"titular": "Depois", "cpf": "12345678901", "chavePix": "{{novaChave}}", "saldo": 999}""");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Json();
        Assert.Equal(("Depois", novaChave, 10m), ((string)corpo["titular"]!, (string)corpo["chavePix"]!, (decimal)corpo["saldo"]!));
    }

    [Fact]
    public async Task Put_ParaChaveDeOutraConta_DeveRetornar409()
    {
        var a = HttpExtensions.ChaveUnica();
        var b = HttpExtensions.ChaveUnica();
        await _http.CriarConta(a, "1");
        var id = (long)(await (await _http.PostJson("/contas",
            $$"""{"titular": "B", "cpf": "12345678901", "chavePix": "{{b}}", "saldo": 1}""")).Json())["id"]!;

        var resposta = await _http.PutJson($"/contas/{id}",
            $$"""{"titular": "B", "cpf": "12345678901", "chavePix": "{{a}}", "saldo": 1}""");

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Delete_DeveRetornar204_EDepois404()
    {
        var id = (long)(await (await _http.PostJson("/contas",
            $$"""{"titular": "X", "cpf": "12345678901", "chavePix": "{{HttpExtensions.ChaveUnica()}}", "saldo": 5}""")).Json())["id"]!;

        Assert.Equal(HttpStatusCode.NoContent, (await _http.DeleteAsync($"/contas/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.DeleteAsync($"/contas/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/contas/{id}")).StatusCode);
    }
}
