using System.Globalization;
using System.Text;

namespace PixApi.Application.Batch;

// Layout do arquivo MOVIMENTO: registros de 150 bytes, campos em posições fixas.
// É o mesmo contrato do copybook cobol/copybooks/MOVIMENTO.cpy do pix-api — se um mudar, o outro muda junto.
//
//  H  1     tipo 'H' | 2-9 data-ref AAAAMMDD | 10-23 gerado-em AAAAMMDDhhmmss | 24-33 sistema
//  C  1     tipo 'C' | 2-11 conta-id 9(10) | 12-88 chave X(77) | 89-118 titular X(30) | 119-133 saldo 9(13)V99
//  L  1     tipo 'L' | 2-11 conta-id | 12-19 data | 20-25 hora | 26 natureza C/D | 27-38 tipo X(12)
//                    | 39-53 valor 9(13)V99 | 54-89 transferencia-id X(36)
//  T  1     tipo 'T' | 2-10 qtd-contas 9(9) | 11-19 qtd-lancamentos 9(9)
//                    | 20-36 total-creditos 9(15)V99 | 37-53 total-debitos 9(15)V99
//
// "9(13)V99": 13 dígitos inteiros + 2 decimais, com a vírgula IMPLÍCITA. R$ 1.234,50 vira "000000000123450".
public static class LayoutMovimento
{
    public const int TamanhoRegistro = 150;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Header(DateOnly dataReferencia, DateTime geradoEm) =>
        Registro("H" + dataReferencia.ToString("yyyyMMdd", Inv) + geradoEm.ToString("yyyyMMddHHmmss", Inv)
                 + Texto("PIX-API", 10));

    public static string Conta(long id, string chavePix, string titular, decimal saldo) =>
        Registro("C" + Numero(id, 10) + Texto(chavePix, 77) + Texto(titular, 30) + Valor(saldo, 15));

    public static string Lancamento(long contaId, DateTime quando, bool credito, string tipo, decimal valor,
                                    Guid? transferenciaId) =>
        Registro("L" + Numero(contaId, 10) + quando.ToString("yyyyMMdd", Inv) + quando.ToString("HHmmss", Inv)
                 + (credito ? "C" : "D") + Texto(tipo, 12) + Valor(valor, 15)
                 + Texto(transferenciaId?.ToString() ?? "", 36));

    public static string Trailer(long qtdContas, long qtdLancamentos, decimal creditos, decimal debitos) =>
        Registro("T" + Numero(qtdContas, 9) + Numero(qtdLancamentos, 9) + Valor(creditos, 17) + Valor(debitos, 17));

    // Completa com espaços até 150 (o FILLER do COBOL).
    private static string Registro(string conteudo)
    {
        if (conteudo.Length > TamanhoRegistro)
        {
            throw new InvalidOperationException($"registro maior que {TamanhoRegistro}: {conteudo.Length}");
        }
        return conteudo.PadRight(TamanhoRegistro);
    }

    // PIC 9(n): zeros à esquerda ("D10" = 10 dígitos).
    private static string Numero(long valor, int tamanho) => valor.ToString("D" + tamanho, Inv);

    // PIC 9(n)V99: valor em centavos, sem vírgula. Recusa mais de 2 casas em vez de arredondar
    // (o Java usa RoundingMode.UNNECESSARY pelo mesmo motivo).
    private static string Valor(decimal valor, int tamanho)
    {
        var centavos = valor * 100;
        if (centavos != decimal.Truncate(centavos))
        {
            throw new InvalidOperationException($"valor com mais de 2 casas decimais: {valor.ToString(Inv)}");
        }
        return Numero((long)centavos, tamanho);
    }

    // PIC X(n): ASCII, alinhado à esquerda, completado com espaços ou truncado.
    // Acentos viram letras simples ("ç" → "c"): em UTF-8 eles ocupam 2 bytes e deslocariam
    // todas as posições seguintes, porque o COBOL lê o registro byte a byte.
    private static string Texto(string valor, int tamanho)
    {
        var resultado = new StringBuilder(tamanho);
        // FormD separa a letra do acento ("ç" → "c" + cedilha combinante).
        // EnumerateRunes percorre "code points": um emoji (2 chars em UTF-16) vira um "?", não dois.
        foreach (var rune in valor.Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            var categoria = Rune.GetUnicodeCategory(rune);
            if (categoria is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }
            resultado.Append(rune.Value is >= 0x20 and <= 0x7E ? (char)rune.Value : '?');
        }
        var texto = resultado.ToString();
        return (texto.Length > tamanho ? texto[..tamanho] : texto).PadRight(tamanho);
    }
}
