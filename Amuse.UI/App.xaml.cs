using Amuse.UI.Commands;
using Amuse.UI.Core.Services;
using Amuse.UI.Dialogs;
using Amuse.UI.Exceptions;
using Amuse.UI.Frontends.Api;
using Amuse.UI.Helpers;
using Amuse.UI.Models;
using Amuse.UI.Services;
using Amuse.UI.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnnxStack.Core;
using OnnxStack.Core.Config;
using OnnxStack.Core.Video;
using OnnxStack.Device.Services;
using Serilog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Amuse.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, INotifyPropertyChanged
    {
        private static readonly string _version = Utils.GetAppVersion();
        private static readonly string _displayVersion = Utils.GetDisplayVersion();
        private const string GitHubReleasesApi = "https://api.github.com/repos/Carnes/AmuseAI/releases/latest";
        private const string GitHubReleasesUrl = "https://github.com/Carnes/AmuseAI/releases";

        private static IHost _applicationHost;
        private static Mutex _applicationMutex;
        private static string _baseDirectory;
        private static string _dataDirectory;
        private static string _tempDirectory;
        private static string _cacheDirectory;
        private static string _pluginsDirectory;
        private static string _logDirectory;
        private static bool _isHeadlessMode;

        private readonly ILogger<App> _logger;
        private readonly Splashscreen _splashscreen = new();
        private readonly HttpClient _httpClient;
        private bool _isGenerating;
        private bool _isUpdateAvailable;
        private string _latestVersion;
        private AmuseSettings _amuseSettings;
        private IFileService _fileService;
        private IDialogService _dialogService;
        private IHardwareService _hardwareService;
        private ApiHostService _apiHostService;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public App()
        {
            // Check for headless mode first
            var args = Environment.GetCommandLineArgs();
            _isHeadlessMode = args.Contains("--no-ui", StringComparer.OrdinalIgnoreCase) ||
                              args.Contains("--headless", StringComparer.OrdinalIgnoreCase);

            _applicationMutex = new Mutex(false, "Global\\TensorStack_Amuse", out bool isNewInstance);
            if (!isNewInstance)
            {
                ActivateExistingInstance();
                Environment.Exit(0);
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AmuseAI");
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _pluginsDirectory = Path.Combine(_baseDirectory, "Plugins");
            _dataDirectory = GetApplicationDataDirectory();
            _tempDirectory = Path.Combine(_dataDirectory, "Temp");
            _cacheDirectory = Path.Combine(_dataDirectory, "Cache");
            _logDirectory = Path.Combine(_dataDirectory, "Logs");

            var settings = SettingsManager.LoadSettings();
            ConfigManager.SetConfiguration(_dataDirectory);

            Initialize(settings);
            _logger = GetService<ILogger<App>>();
            UpdateCommand = new AsyncRelayCommand(UpdateAsync);
            DispatcherUnhandledException += Application_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        public static string Version => _version;
        public static string DisplayVersion => _displayVersion;
        public static string BaseDirectory => _baseDirectory;
        public static string DataDirectory => _dataDirectory;
        public static string PluginDirectory => _pluginsDirectory;
        public static string TempDirectory => _tempDirectory;
        public static string CacheDirectory => _cacheDirectory;
        public static string LogDirectory => _logDirectory;
        public static bool IsHeadlessMode => _isHeadlessMode;

        /// <summary>
        /// Gets the application data directory, if Installer build use LocalApplicationData, else just executable directory
        /// </summary>
        /// <returns></returns>
        public static string GetApplicationDataDirectory()
        {
            return _baseDirectory;
        }

        public static BaseWindow CurrentWindow => Current.MainWindow as BaseWindow;
        public static T GetService<T>() => _applicationHost.Services.GetService<T>();
        public static void UIInvoke(Action action, DispatcherPriority priority = DispatcherPriority.Normal) => Current.Dispatcher.BeginInvoke(priority, action);
        public static DispatcherOperation UIInvokeAsync(Func<Task> value, DispatcherPriority priority = DispatcherPriority.Normal) => Current.Dispatcher.InvokeAsync(value, priority);

        public static async Task<T> UIInvokeAsync<T>(Func<Task<T>> function, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            return await await Current.Dispatcher.InvokeAsync(function);
        }


        private static void Initialize(AmuseSettings amuseSettings)
        {
            var builder = Host.CreateApplicationBuilder();

            // Add Logging
            builder.Logging.ClearProviders();
            builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .WriteTo.File(GetLogId(), rollOnFileSizeLimit: true)
                .WriteTo.Sink(LogSinkService.Instance));

            // Add OnnxStack
            builder.Services.AddOnnxStack();
            builder.Services.AddSingleton(amuseSettings);
            builder.Services.AddSingleton<IHardwareSettings>(amuseSettings);

            // Add Windows
            builder.Services.AddSingleton<MainWindow>();

            // Dialogs
            builder.Services.AddTransient<MessageDialog>();
            builder.Services.AddTransient<TextInputDialog>();
            builder.Services.AddTransient<CropImageDialog>();
            builder.Services.AddTransient<AddModelDialog>();
            builder.Services.AddTransient<UpdateModelDialog>();
            builder.Services.AddTransient<AddUpscaleModelDialog>();
            builder.Services.AddTransient<UpdateUpscaleModelDialog>();
            builder.Services.AddTransient<UpdateModelMetadataDialog>();
            builder.Services.AddTransient<ViewModelMetadataDialog>();
            builder.Services.AddTransient<AddControlNetModelDialog>();
            builder.Services.AddTransient<UpdateControlNetModelDialog>();
            builder.Services.AddTransient<AddFeatureExtractorModelDialog>();
            builder.Services.AddTransient<UpdateFeatureExtractorModelDialog>();
            builder.Services.AddTransient<PreviewImageDialog>();
            builder.Services.AddTransient<PreviewVideoDialog>();
            builder.Services.AddTransient<AddPromptInputDialog>();
            builder.Services.AddTransient<ModelDownloadDialog>();
            builder.Services.AddTransient<AppUpdateDialog>();
            builder.Services.AddTransient<CreateVideoDialog>();
            builder.Services.AddTransient<ModelLicenceDialog>();

            // Services
            builder.Services.AddSingleton<IModelFactory, ModelFactory>();
            builder.Services.AddSingleton<IDialogService, DialogService>();
            builder.Services.AddSingleton<IModelDownloadService, ModelDownloadService>();
            builder.Services.AddSingleton<IDeviceService, DeviceService>();
            builder.Services.AddSingleton<IFileService, FileService>();
            builder.Services.AddSingleton<IModelCacheService, ModelCacheService>();
            builder.Services.AddSingleton<IModeratorService, ModeratorService>();
            builder.Services.AddSingleton<IPreviewService, PreviewService>();
            builder.Services.AddSingleton<IHardwareService, HardwareService>();
            builder.Services.AddSingleton<IProviderService, ProviderService>();

            // Core Services (shared by all frontends: UI, API, Discord, etc.)
            builder.Services.AddSingleton<IGenerationService, GenerationService>();
            builder.Services.AddSingleton<IJobQueueService, JobQueueService>();

            // API Frontend (manually controlled based on ApiIsEnabled setting)
            builder.Services.AddSingleton<ApiHostService>();

            // Build App
            _applicationHost = builder.Build();
        }

        public AsyncRelayCommand UpdateCommand { get; set; }


        public bool IsUpdateAvailable
        {
            get { return _isUpdateAvailable; }
            set { _isUpdateAvailable = value; NotifyPropertyChanged(); }
        }

        public bool IsGenerating
        {
            get { return _isGenerating; }
            set { _isGenerating = value; NotifyPropertyChanged(); }
        }


        /// <summary>
        /// Raises the <see cref="E:Startup" /> event.
        /// </summary>
        /// <param name="e">The <see cref="StartupEventArgs"/> instance containing the event data.</param>
        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                _logger.LogInformation("[OnStartup] - ApplicationHost Starting... (Headless: {IsHeadless})", _isHeadlessMode);
                await _applicationHost.StartAsync();

                // Load Config
                _logger.LogInformation("[OnStartup] - Loading Configuration...");
                var settings = GetService<OnnxStackConfig>();
                _amuseSettings = GetService<AmuseSettings>();
                _fileService = GetService<IFileService>();
                _dialogService = GetService<IDialogService>();

                _amuseSettings.HasExited = false;
                settings.TempPath = _tempDirectory;
                VideoHelper.SetConfiguration(settings);

                _logger.LogInformation("[OnStartup] - Query Device Configuration...");
                _hardwareService = GetService<IHardwareService>();
                GetService<IDeviceService>();

                // Get API service for manual control
                _apiHostService = GetService<ApiHostService>();

                // In headless mode, skip UI initialization but always start API
                if (_isHeadlessMode)
                {
                    _splashscreen.Close();
                    _logger.LogInformation("[OnStartup] - Running in headless API mode");
                    await _apiHostService.StartAsync(CancellationToken.None);
                    _logger.LogInformation($"[OnStartup] - Amuse {App.Version} started in headless mode");

                    // Keep app running without UI
                    base.OnStartup(e);
                    return;
                }

                // Start API if enabled
                if (_amuseSettings.ApiIsEnabled)
                {
                    _logger.LogInformation("[OnStartup] - Starting API server (enabled in settings)...");
                    await _apiHostService.StartAsync(CancellationToken.None);
                }

                // Set RenderMode (only in UI mode)
                RenderOptions.ProcessRenderMode = _amuseSettings.RenderMode;

                // Preload Windows
                _logger.LogInformation("[OnStartup] - Launch UI...");
                var window = GetService<MainWindow>();

                _splashscreen.Close();

                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                await window.ShowAsync(WindowState.Normal);
                MainWindow = window;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                MainWindow.Activate();

                base.OnStartup(e);

                await _amuseSettings.SaveAsync();
                _amuseSettings.NotifyPropertyChanged(nameof(_amuseSettings.DefaultExecutionDevice));
                _logger.LogInformation($"[OnStartup] - Amuse {App.Version} successfully started");

                // Check for updates if enabled
                if (_amuseSettings.IsUpdateEnabled)
                {
                    _ = CheckForUpdatesAsync(); // Fire and forget to not block startup
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OnStartup] - Application Failed to start.");
                Environment.Exit(1);
            }
        }


        /// <summary>
        /// Raises the <see cref="E:System.Windows.Application.SessionEnding" /> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.SessionEndingCancelEventArgs" /> that contains the event data.</param>
        protected override async void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            _logger.LogInformation($"[OnSessionEnding] - Application Exit, Reason: {e.ReasonSessionEnding}");
            await AppShutdown();
            base.OnSessionEnding(e);
        }


        /// <summary>
        /// Raises the <see cref="E:System.Windows.Application.Exit" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.Windows.ExitEventArgs" /> that contains the event data.</param>
        protected override async void OnExit(ExitEventArgs e)
        {
            _logger.LogInformation($"[OnExit] - Application Exit, ExitCode: {e.ApplicationExitCode}");
            await AppShutdown();
            base.OnExit(e);
        }


        /// <summary>
        /// Raises the <see cref="E:System.Windows.Application.Activated" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> that contains the event data.</param>
        protected override void OnActivated(EventArgs e)
        {
            //_logger.LogInformation("[OnActivated] - Resuming Hardware Info.");
            //_hardwareService?.Resume();
            base.OnActivated(e);
        }


        /// <summary>
        /// Raises the <see cref="E:System.Windows.Application.Deactivated" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> that contains the event data.</param>
        protected override void OnDeactivated(EventArgs e)
        {
            //_logger.LogInformation("[OnDeactivated] - Pausing Hardware Info.");
            //_hardwareService?.Pause();
            base.OnDeactivated(e);
        }


        /// <summary>
        /// Application shutdown.
        /// </summary>
        private async Task AppShutdown()
        {
            if (_amuseSettings.HasExited)
                return;

            _logger.LogInformation("[AppShutdown] - Application Shutdown");
            _amuseSettings.HasExited = true;
            await _amuseSettings.SaveAsync();
            await _fileService.DeleteTempFiles();

            // Stop API server if running
            if (_apiHostService?.IsRunning == true)
            {
                await _apiHostService.StopAsync(CancellationToken.None);
            }

            await _applicationHost.StopAsync();
            await _cancellationTokenSource.CancelAsync();
            _applicationHost.Dispose();
            _applicationMutex.WaitOne();
            _applicationMutex.ReleaseMutex();
            _applicationMutex.Dispose();
        }


        private static string GetLogId()
        {
            var now = DateTime.Now;
            return Path.Combine(DataDirectory, @$"Logs\Amuse-{DateTime.Now.ToString("dd-MM-yyyy")}-{now.Hour * 3600 + now.Minute * 60 + now.Second}.txt");
        }


        private void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger.LogError("[UnhandledException] - An Unhandled Exception Occured. {ExceptionObject}", e.ExceptionObject);
            Log.CloseAndFlush();
            Current.Shutdown();
        }


        private async void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger.LogError(e.Exception, "[UnobservedTaskException] - An Unhandled Exception Occured.");

            e.SetObserved();
            await TryShowErrorMessage("TaskScheduler Error", e.Exception?.InnerException?.Message ?? e.Exception?.Message ?? "An Unobserved Task Exception Occured");
        }


        private async void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _logger.LogError(e.Exception, "[DispatcherUnhandledException] - An Unhandled Exception Occured.");

            e.Handled = true;
            if (await HandleUnrecoverableException(e.Exception))
                return;

            //await TryShowErrorMessage("Dispatcher Error", e.Exception?.InnerException?.Message ?? e.Exception?.Message ?? "An Unhandled Dispatcher Exception Occured");
        }


        private async Task TryShowErrorMessage(string title, string message)
        {
            try
            {
                await UIInvokeAsync(() =>
                {
                    return _dialogService.ShowErrorMessageAsync(title, message);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TryShowErrorMessage] - Failed to show error dialog.");
            }
        }


        /// <summary>
        /// Handles unrecoverable exceptions.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <returns></returns>
        private async Task<bool> HandleUnrecoverableException(Exception exception)
        {
            if (exception is UnrecoverableException)
            {
                await TryShowErrorMessage("OnnxRuntime Error", "ONNX Runtime failed to initialize!\n\nTo ensure optimal performance, Amuse needs to restart.");
                await RestartApplication();
                return true;
            }
            return false;
        }


        /// <summary>
        /// Restarts the application.
        /// </summary>
        private async Task RestartApplication()
        {
            _logger.LogInformation("[RestartApplication] - Amuse is restarting...");
            await AppShutdown();
            Log.CloseAndFlush();
            Process.Start(Path.Combine(_baseDirectory, "Amuse.exe"));
            Environment.Exit(0);
        }


        [DllImport("USER32.DLL")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        private void ActivateExistingInstance()
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(currentProcess.ProcessName);
            foreach (var process in processes)
            {
                if (process.Id != currentProcess.Id)
                {
                    SetForegroundWindow(process.MainWindowHandle);
                    break;
                }
            }
        }

        private Task UpdateAsync()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = GitHubReleasesUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UpdateAsync] - Failed to open releases page.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks for updates from GitHub releases.
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                _logger.LogInformation("[CheckForUpdatesAsync] - Checking for updates...");

                var response = await _httpClient.GetAsync(GitHubReleasesApi);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[CheckForUpdatesAsync] - Failed to check for updates. Status: {StatusCode}", response.StatusCode);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("tag_name", out var tagElement))
                {
                    _latestVersion = tagElement.GetString()?.TrimStart('v', 'V') ?? string.Empty;
                    var currentVersion = _version.TrimStart('v', 'V');

                    _logger.LogInformation("[CheckForUpdatesAsync] - Current version: {CurrentVersion}, Latest version: {LatestVersion}", currentVersion, _latestVersion);

                    if (System.Version.TryParse(_latestVersion, out var latest) && System.Version.TryParse(currentVersion, out var current))
                    {
                        if (latest > current)
                        {
                            _logger.LogInformation("[CheckForUpdatesAsync] - Update available: {LatestVersion}", _latestVersion);
                            IsUpdateAvailable = true;
                        }
                        else
                        {
                            _logger.LogInformation("[CheckForUpdatesAsync] - Application is up to date.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CheckForUpdatesAsync] - Failed to check for updates.");
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
