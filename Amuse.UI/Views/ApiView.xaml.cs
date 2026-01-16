using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Amuse.UI.Commands;
using Amuse.UI.Core.Models;
using Amuse.UI.Core.Services;
using Amuse.UI.Frontends.Api;
using Amuse.UI.Models;
using Amuse.UI.Services;

namespace Amuse.UI.Views
{
    /// <summary>
    /// API monitoring view showing job queue and logs.
    /// </summary>
    public partial class ApiView : UserControl, INotifyPropertyChanged
    {
        private readonly IJobQueueService _jobQueueService;
        private readonly ApiHostService _apiHostService;
        private GenerationJob _selectedJob;

        public ApiView()
        {
            InitializeComponent();

            _jobQueueService = App.GetService<IJobQueueService>();
            _apiHostService = App.GetService<ApiHostService>();

            // Subscribe to job queue events
            if (_jobQueueService != null)
            {
                _jobQueueService.JobStatusChanged += OnJobStatusChanged;
                _jobQueueService.JobProgressChanged += OnJobProgressChanged;
                RefreshJobs();
            }

            // Subscribe to log updates for auto-scroll
            LogSinkService.Instance.PropertyChanged += OnLogPropertyChanged;

            // Refresh API status when view is loaded
            Loaded += OnLoaded;

            ClearCompletedCommand = new AsyncRelayCommand(ClearCompleted);
            ClearLogsCommand = new AsyncRelayCommand(ClearLogs);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Refresh API status properties
            NotifyPropertyChanged(nameof(IsApiRunning));
            NotifyPropertyChanged(nameof(ListeningUrl));
        }

        public AmuseSettings Settings
        {
            get { return (AmuseSettings)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }
        public static readonly DependencyProperty SettingsProperty =
            DependencyProperty.Register("Settings", typeof(AmuseSettings), typeof(ApiView));

        public AsyncRelayCommand ClearCompletedCommand { get; }
        public AsyncRelayCommand ClearLogsCommand { get; }

        public ObservableCollection<GenerationJob> Jobs { get; } = new ObservableCollection<GenerationJob>();

        public ObservableCollection<LogEntry> LogEntries => LogSinkService.Instance.LogEntries;

        public GenerationJob SelectedJob
        {
            get => _selectedJob;
            set
            {
                _selectedJob = value;
                NotifyPropertyChanged();
            }
        }

        public bool IsApiRunning => _apiHostService?.IsRunning ?? false;

        public string ListeningUrl => _apiHostService?.ListeningUrl ?? string.Empty;

        private void OnJobStatusChanged(object sender, JobStatusChangedEventArgs e)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                RefreshJobs();
                NotifyPropertyChanged(nameof(Jobs));
            }));
        }

        private void OnJobProgressChanged(object sender, JobProgressEventArgs e)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                NotifyPropertyChanged(nameof(Jobs));
            }));
        }

        private void OnLogPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LogSinkService.LogEntries))
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    NotifyPropertyChanged(nameof(LogEntries));

                    // Auto-scroll to bottom
                    if (LogListBox.Items.Count > 0)
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                    }
                }));
            }
        }

        private void RefreshJobs()
        {
            Jobs.Clear();
            var jobs = _jobQueueService?.GetJobs();
            if (jobs != null)
            {
                foreach (var job in jobs)
                {
                    Jobs.Add(job);
                }
            }
        }

        private System.Threading.Tasks.Task ClearCompleted()
        {
            _jobQueueService?.ClearCompletedJobs();
            RefreshJobs();
            NotifyPropertyChanged(nameof(Jobs));
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private System.Threading.Tasks.Task ClearLogs()
        {
            LogSinkService.Instance.Clear();
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private void JobId_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBlock textBlock && textBlock.Tag is Guid jobId)
            {
                Clipboard.SetText(jobId.ToString());
            }
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
        #endregion
    }
}
