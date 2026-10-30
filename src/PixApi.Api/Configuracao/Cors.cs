using System.Text.RegularExpressions;

namespace PixApi.Api.Configuracao;

// CORS: o navegador bloqueia JavaScript de um domínio (github.io) chamando outro (onrender.com),
// a menos que o servidor diga quais origens confia. Mesmas regras do CorsConfig do Java.
public static class Cors
{
    public const string Politica = "front-end";

    private const string OrigensPadrao = "https://rafaelnicolauios.github.io,http://localhost:*,http://127.0.0.1:*";

    public static IServiceCollection AdicionarCors(this IServiceCollection servicos, IConfiguration config)
    {
        // "Pix:Cors:Origens" ou a variável PIX_CORS_ORIGENS (a mesma do Java), separadas por vírgula.
        var origens = (config["PIX_CORS_ORIGENS"] ?? config["Pix:Cors:Origens"] ?? OrigensPadrao)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParaRegex)
            .ToArray();

        servicos.AddCors(opcoes => opcoes.AddPolicy(Politica, politica => politica
            .SetIsOriginAllowed(origem => origens.Any(r => r.IsMatch(origem)))
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .WithHeaders("Content-Type", "X-Admin-Token")
            .SetPreflightMaxAge(TimeSpan.FromHours(1)))); // o navegador guarda o preflight por 1 h
        return servicos;
    }

    // "http://localhost:*" → ^http://localhost:\d+$ (o allowedOriginPatterns do Spring).
    // Tudo é escapado: "http://localhost.evil.com:8000" não casa.
    private static Regex ParaRegex(string padrao) =>
        new("^" + Regex.Escape(padrao).Replace(@"\*", @"\d+") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
