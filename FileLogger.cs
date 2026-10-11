using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

/// <summary>
/// Minimal daily log file (logs/app-yyyyMMdd.log, newest 30 kept). Warnings and errors from the framework,
/// plus everything from the app, so problems are on record instead of only in a console window.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _dir;
    private readonly object _lock = new();

    public FileLoggerProvider(string directory)
    {
        _dir = directory;
        try
        {
            Directory.CreateDirectory(_dir);
            foreach (var old in new DirectoryInfo(_dir).GetFiles("app-*.log").OrderByDescending(f => f.Name).Skip(30))
                old.Delete();
        }
        catch { /* logging must never stop the app */ }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    public void Dispose() { }

    private void Write(string line)
    {
        try
        {
            lock (_lock)
                File.AppendAllText(Path.Combine(_dir, $"app-{DateTime.UtcNow:yyyyMMdd}.log"), line + Environment.NewLine);
        }
        catch { /* ignore */ }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly FileLoggerProvider _owner;

        public FileLogger(string category, FileLoggerProvider owner) { _category = category; _owner = owner; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel level) =>
            level != LogLevel.None &&
            (_category.StartsWith("Microsoft", StringComparison.Ordinal) || _category.StartsWith("System", StringComparison.Ordinal)
                ? level >= LogLevel.Warning
                : level >= LogLevel.Information);

        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            string text = $"{DateTime.UtcNow:O} [{level}] {_category}: {formatter(state, exception)}";
            if (exception != null) text += Environment.NewLine + exception;
            _owner.Write(text);
        }
    }
}
