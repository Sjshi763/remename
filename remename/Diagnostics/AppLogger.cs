using System;
using System.Diagnostics;
using System.IO;

namespace remename.Diagnostics;

public static class AppLogger
{
    private static readonly object SyncRoot = new();
    private static readonly TraceSource Source = new("remename", SourceLevels.All);
    private static bool _initialized;

    public static string? LogFilePath { get; private set; }

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
                return;

            _initialized = true;
            Source.Listeners.Clear();
            Source.Listeners.Add(new DefaultTraceListener());

            try
            {
                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "remename",
                    "logs");
                Directory.CreateDirectory(logDirectory);

                LogFilePath = Path.Combine(logDirectory, $"remename-{DateTime.UtcNow:yyyyMMdd}.log");
                Source.Listeners.Add(new TextWriterTraceListener(LogFilePath));
            }
            catch (Exception ex)
            {
                Source.TraceEvent(TraceEventType.Warning, 0, $"File logging unavailable: {ex.Message}");
            }

            Info("Application logging initialized");
        }
    }

    public static void Info(string message) => Write(TraceEventType.Information, message);

    public static void Warning(string message) => Write(TraceEventType.Warning, message);

    public static void Error(string message, Exception? exception = null)
    {
        var detail = exception == null ? message : $"{message}{Environment.NewLine}{exception}";
        Write(TraceEventType.Error, detail);
    }

    private static void Write(TraceEventType eventType, string message)
    {
        if (!_initialized)
            Initialize();

        lock (SyncRoot)
        {
            Source.TraceEvent(eventType, 0, $"{DateTimeOffset.Now:O} [{eventType}] {message}");
            Source.Flush();
        }
    }
}
