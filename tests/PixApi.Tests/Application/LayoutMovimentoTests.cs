using PixApi.Application.Batch;

namespace PixApi.Tests.Application;

// O layout é o contrato com o COBOL (copybook MOVIMENTO.cpy): cada campo numa posição fixa.
// Mesmos casos do LayoutMovimentoTest do Java, posição por posição.
public class LayoutMovimentoTests
{
    // Posições "humanas" (1-based, inclusivas), como na documentação de layout de mainframe.
    private static string Campo(string registro, int de, int ate) => registro.Substring(de - 1, ate - de + 1);

    private static bool SoAscii(string s) => s.All(c => c <= 0x7F);

    [Fact]
    public void Header_DeveTerDataDeReferenciaGeracaoESistema()
    {
        var h = LayoutMovimento.Header(new DateOnly(2026, 10, 25), new DateTime(2026, 10, 26, 3, 0, 5));

        Assert.Equal(150, h.Length);
        Assert.Equal("H", Campo(h, 1, 1));
        Assert.Equal("20261025", Campo(h, 2, 9));
        Assert.Equal("20261026030005", Campo(h, 10, 23));
        Assert.Equal("PIX-API   ", Campo(h, 24, 33));
        Assert.True(string.IsNullOrWhiteSpace(Campo(h, 34, 150)));
    }

    [Fact]
    public void Conta_DeveAlinharCamposERemoverAcentos()
    {
        var c = LayoutMovimento.Conta(42, "conceição@pix.com", "Maria da Conceição Ávila", 1234.5m);

        Assert.Equal(150, c.Length);
        Assert.Equal("C", Campo(c, 1, 1));
        Assert.Equal("0000000042", Campo(c, 2, 11));
        Assert.Equal("conceicao@pix.com".PadRight(77), Campo(c, 12, 88));
        Assert.Equal("Maria da Conceicao Avila".PadRight(30), Campo(c, 89, 118));
        Assert.Equal("000000000123450", Campo(c, 119, 133)); // 9(13)V99: vírgula implícita
        Assert.True(SoAscii(c));
    }

    [Fact]
    public void Conta_DeveTruncarTitularLongo()
    {
        var c = LayoutMovimento.Conta(1, "a@pix.com", "Nome Muito Comprido Que Passa De Trinta Caracteres", 10m);

        Assert.Equal("Nome Muito Comprido Que Passa ", Campo(c, 89, 118));
        Assert.Equal(150, c.Length);
    }

    [Fact]
    public void Conta_EmojiViraUmaInterrogacao_ENaoDuas()
    {
        // Um emoji ocupa 2 "char" (par substituto UTF-16) em C#, mas é 1 caractere para o usuário.
        var c = LayoutMovimento.Conta(1, "a@pix.com", "Ana 😀 Souza", 1m);

        Assert.Equal("Ana ? Souza".PadRight(30), Campo(c, 89, 118));
    }

    [Fact]
    public void Lancamento_DeveTerNaturezaTipoValorEId()
    {
        var id = Guid.Parse("6f1b7c1e-0000-4000-8000-000000000001");
        var l = LayoutMovimento.Lancamento(7, new DateTime(2026, 10, 25, 14, 30, 9), true, "PIX_RECEBIDO", 30.00m, id);

        Assert.Equal(150, l.Length);
        Assert.Equal("L", Campo(l, 1, 1));
        Assert.Equal("0000000007", Campo(l, 2, 11));
        Assert.Equal("20261025", Campo(l, 12, 19));
        Assert.Equal("143009", Campo(l, 20, 25));
        Assert.Equal("C", Campo(l, 26, 26));
        Assert.Equal("PIX_RECEBIDO", Campo(l, 27, 38));
        Assert.Equal("000000000003000", Campo(l, 39, 53));
        Assert.Equal(id.ToString(), Campo(l, 54, 89));
    }

    [Fact]
    public void Lancamento_SemTransferencia_DeixaIdEmBranco()
    {
        var l = LayoutMovimento.Lancamento(7, new DateTime(2026, 10, 25, 8, 0, 0), false, "ENCERRAMENTO", 1m, null);

        Assert.Equal("D", Campo(l, 26, 26));
        Assert.True(string.IsNullOrWhiteSpace(Campo(l, 54, 89)));
    }

    [Fact]
    public void Trailer_DeveTerQuantidadesETotaisDeControle()
    {
        var t = LayoutMovimento.Trailer(3, 12, 1500.25m, 200.00m);

        Assert.Equal(150, t.Length);
        Assert.Equal("T", Campo(t, 1, 1));
        Assert.Equal("000000003", Campo(t, 2, 10));
        Assert.Equal("000000012", Campo(t, 11, 19));
        Assert.Equal("00000000000150025", Campo(t, 20, 36));
        Assert.Equal("00000000000020000", Campo(t, 37, 53));
    }

    [Fact]
    public void Valor_ComMaisDeDuasCasas_DeveSerRecusado()
    {
        // Equivale ao RoundingMode.UNNECESSARY do Java: arredondar em silêncio esconderia um bug.
        Assert.Throws<InvalidOperationException>(() => LayoutMovimento.Conta(1, "a@pix.com", "Ana", 0.001m));
    }
}
