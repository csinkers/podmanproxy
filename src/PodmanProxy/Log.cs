using Microsoft.Extensions.Logging;

namespace PodmanProxy;

public class ConsoleLogger : ILogger
{
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    void Write(LogLevel level, string message)
    {
        if (level < LogLevel)
            return;

        (string severity, ConsoleColor color) = level switch
        {
            LogLevel.Critical => ("FATAL", ConsoleColor.Magenta),
            LogLevel.Error => ("ERROR", ConsoleColor.Red),
            LogLevel.Warning => ("WARN ", ConsoleColor.Yellow),
            LogLevel.Information => ("INFO ", ConsoleColor.White),
            LogLevel.Debug => ("DEBUG", ConsoleColor.Gray),
            _ => ("MISC ", ConsoleColor.White)
        };

        var oldColor = Console.ForegroundColor;
        if (oldColor == color)
        {
            Console.WriteLine($"{severity} {message}");
            return;
        }

        Console.ForegroundColor = color;
        Console.WriteLine($"{severity} {message}");
        Console.ForegroundColor = oldColor;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    ) => Write(logLevel, formatter(state, exception));

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
