using Microsoft.Extensions.Logging;

namespace PixApi.Infrastructure.Monitoramento;

// Uma linha de log já "traduzida" para JSON. Logger traz só o nome curto da classe.
public sealed record LinhaDeLog(DateTimeOffset Instante, string Nivel, string Logger, string Mensagem);

// Guarda as últimas linhas de log em memória para o GET /logs.
// No .NET, um ILoggerProvider é um destino de log (console, arquivo...), o "appender" do Logback.
// Este é registrado ao lado do console e copia cada linha para um buffer circular.
public sealed class LogsEmMemoria(int capacidade) : ILoggerProvider
{
    private readonly LinkedList<LinhaDeLog> _linhas = new();

    public ILogger CreateLogger(string categoria) => new Logger(this, categoria[(categoria.LastIndexOf('.') + 1)..]);

    // As "limite" linhas mais recentes, em ordem cronológica (a mais nova por último, como num tail).
    public IReadOnlyList<LinhaDeLog> Ultimas(int limite)
    {
        lock (_linhas)
        {
            return _linhas.Skip(Math.Max(0, _linhas.Count - limite)).ToList();
        }
    }

    private void Adicionar(LinhaDeLog linha)
    {
        lock (_linhas)
        {
            _linhas.AddLast(linha);
            while (_linhas.Count > capacidade)
            {
                _linhas.RemoveFirst();
            }
        }
    }

    public void Dispose()
    {
    }

    // Mesmos nomes de nível do Logback, para a página Atividade não precisar mudar.
    private static string Nivel(LogLevel nivel) => nivel switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        _ => "ERROR",
    };

    private sealed class Logger(LogsEmMemoria destino, string nomeCurto) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel nivel) => nivel != LogLevel.None;

        public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? erro,
                                Func<TState, Exception?, string> formatar)
        {
            var mensagem = formatar(estado, erro);
            if (erro is not null)
            {
                // Só a exceção e a causa, sem o stack trace inteiro (que continua no console).
                mensagem += $" | {erro.GetType().FullName}: {erro.Message}";
                if (erro.InnerException is { } causa)
                {
                    mensagem += $" | causa: {causa.GetType().FullName}: {causa.Message}";
                }
            }
            destino.Adicionar(new LinhaDeLog(DateTimeOffset.UtcNow, Nivel(nivel), nomeCurto, mensagem));
        }
    }
}
