using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Serilog.Core;
using Serilog.Events;

namespace Amuse.UI.Services
{
    /// <summary>
    /// In-memory log sink service that collects log entries for UI display.
    /// </summary>
    public class LogSinkService : ILogEventSink, INotifyPropertyChanged
    {
        private static LogSinkService _instance;
        private readonly object _lock = new object();
        private const int MaxLogEntries = 1000;

        public static LogSinkService Instance => _instance ??= new LogSinkService();

        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        private LogSinkService() { }

        private static readonly string[] ApiRelatedSources = new[]
        {
            "JobQueueService",
            "GenerationService",
            "ApiHostService",
            "GenerateController",
            "JobsController",
            "HealthController",
            "StableDiffusionImageViewBase"
        };

        private static readonly string[] ApiRelatedPrefixes = new[]
        {
            "[JobQueue]",
            "[GenerationService]",
            "[API History]",
            "[ApiHost]",
            "[API]"
        };

        public void Emit(LogEvent logEvent)
        {
            // Extract source context (logger category name)
            string sourceContext = null;
            if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContextValue))
            {
                sourceContext = sourceContextValue.ToString().Trim('"');
            }

            var message = logEvent.RenderMessage();

            // Only collect API-related logs
            if (!IsApiRelated(sourceContext, message))
                return;

            var entry = new LogEntry
            {
                Timestamp = logEvent.Timestamp.LocalDateTime,
                Level = logEvent.Level,
                Message = message,
                Exception = logEvent.Exception?.Message,
                SourceContext = sourceContext
            };

            // Marshal to UI thread
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                lock (_lock)
                {
                    LogEntries.Add(entry);

                    // Keep collection size bounded
                    while (LogEntries.Count > MaxLogEntries)
                    {
                        LogEntries.RemoveAt(0);
                    }
                }

                NotifyPropertyChanged(nameof(LogEntries));
            }));
        }

        private static bool IsApiRelated(string sourceContext, string message)
        {
            // Check source context
            if (!string.IsNullOrEmpty(sourceContext))
            {
                foreach (var source in ApiRelatedSources)
                {
                    if (sourceContext.Contains(source, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // Check message content for API-related prefixes
            if (!string.IsNullOrEmpty(message))
            {
                foreach (var prefix in ApiRelatedPrefixes)
                {
                    if (message.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                lock (_lock)
                {
                    LogEntries.Clear();
                }
                NotifyPropertyChanged(nameof(LogEntries));
            }));
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
        #endregion
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogEventLevel Level { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }
        public string SourceContext { get; set; }

        public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}{(Exception != null ? $" - {Exception}" : "")}";
    }
}
