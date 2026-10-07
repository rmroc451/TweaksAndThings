using System;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace RMROC451.TweaksAndThings;

/// <summary>Isolated Serilog pipeline for this mod; never writes into the game's global logger.</summary>
internal static class ModLog
{
    private static ILogger logger = new LoggerConfiguration()
        .WriteTo.Sink(new DiagnosticSink())
        .CreateLogger();

    internal static void Initialize()
    {
        logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new DiagnosticSink())
            .CreateLogger();
    }

    internal static ILogger ForContext<T>() => logger.ForContext<T>();
    internal static ILogger ForContext(Type type) => logger.ForContext(type);
    internal static void Debug(string template, params object?[] values) => logger.Debug(template, values);
    internal static void Information(string template, params object?[] values) => logger.Information(template, values);
    internal static void Warning(string template, params object?[] values) => logger.Warning(template, values);
    internal static void Warning(Exception exception, string template, params object?[] values) => logger.Warning(exception, template, values);
    internal static void Error(string template, params object?[] values) => logger.Error(template, values);
    internal static void Error(Exception exception, string template, params object?[] values) => logger.Error(exception, template, values);

    private sealed class DiagnosticSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            string context = logEvent.Properties.TryGetValue("SourceContext", out var source)
                ? source.ToString().Trim('"') + ": "
                : string.Empty;
            string message = context + logEvent.RenderMessage();
            if (logEvent.Exception != null) message += Environment.NewLine + logEvent.Exception;
            ModDiagnosticLog.Write(logEvent.Level.ToString().ToUpperInvariant(), message,
                "serilog:" + logEvent.Level + ":" + message, intervalSeconds: 30);
        }
    }
}
