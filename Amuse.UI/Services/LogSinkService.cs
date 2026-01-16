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

        public void Emit(LogEvent logEvent)
        {
            var entry = new LogEntry
            {
                Timestamp = logEvent.Timestamp.LocalDateTime,
                Level = logEvent.Level,
                Message = logEvent.RenderMessage(),
                Exception = logEvent.Exception?.Message
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

        public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}{(Exception != null ? $" - {Exception}" : "")}";
    }
}
