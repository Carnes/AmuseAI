using Amuse.UI.Commands;
using Amuse.UI.Dialogs;
using Amuse.UI.Enums;
using Amuse.UI.Exceptions;
using Amuse.UI.Models;
using Amuse.UI.Models.StableDiffusion;
using Microsoft.Extensions.Logging;
using OnnxStack.StableDiffusion.Common;
using OnnxStack.StableDiffusion.Config;
using OnnxStack.StableDiffusion.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Amuse.UI.Views.EZMode
{
    public partial class GenerateView : EZModeViewBase
    {
        private HardwareProfileGenerate _hardwareProfile;
        private HardwareProfileGroup _profileGroup;
        private HardwareProfileOption _profileOption;
        private HardwareProfileQualityType _selectedModelQuality;
        private HardwareProfileAspectType _selectedResolution;
        private ModelTemplateViewModel _selectedModelTemplate;

        private bool _isNegativePromptEnabled;
        private bool _isPromptEnhanceEnabled = true;
        private bool _isRandomSeedEnabled = true;
        private int _currentSeed;
        private bool _isNPUStableDiffusionEnabled = true;
        private bool _isNPUStableDiffusionVisible;
        private bool _isQualityBoostSupported;
        private bool _isQualityBoostEnabled;
        private DefaultSetting _selectedCoherency;
        private DefaultSetting _selectedVolatility;
        private int _selectedVideoLength = 16;
        private EZModeSettings _generateSettings;
        private int _resultListRows = 0;
        private int _resultListColumns = 4;
        private Dock _resultListPosition = Dock.Bottom;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateView"/> class.
        /// </summary>
        public GenerateView() : base()
        {
            SupportedDiffusers = [DiffuserType.TextToImage];
            ShowImagePreviewCommand = new AsyncRelayCommand<ImageResult>(ShowImagePreview);
            ShowVideoPreviewCommand = new AsyncRelayCommand<VideoResultModel>(ShowVideoPreview);
            SelectResolutionCommand = new RelayCommand<HardwareProfileAspectType>(SelectResolution);
            InfoDialogCommand = new AsyncRelayCommand(InfoDialog);
            SettingsDialogCommand = new AsyncRelayCommand(SettingsDialog);
            ShowNormalModeCommand = new AsyncRelayCommand(ShowNormalMode);
            InitializeComponent();
        }

        public static readonly DependencyProperty SwitchUIModeCommandProperty =
            DependencyProperty.Register("SwitchUIModeCommand", typeof(AsyncRelayCommand<UIModeType>), typeof(GenerateView));
        public override bool IsUpscalerSupported => false;
        public override bool IsControlNetSupported => false;
        public override bool IsFeatureExtractorSupported => false;
        public AsyncRelayCommand<ImageResult> ShowImagePreviewCommand { get; }
        public AsyncRelayCommand<VideoResultModel> ShowVideoPreviewCommand { get; }
        public RelayCommand<HardwareProfileAspectType> SelectResolutionCommand { get; }
        public AsyncRelayCommand InfoDialogCommand { get; }
        public AsyncRelayCommand SettingsDialogCommand { get; }
        public AsyncRelayCommand ShowNormalModeCommand { get; }
        public AsyncRelayCommand<UIModeType> SwitchUIModeCommand
        {
            get { return (AsyncRelayCommand<UIModeType>)GetValue(SwitchUIModeCommandProperty); }
            set { SetValue(SwitchUIModeCommandProperty, value); }
        }

        public HardwareProfileOption ProfileOption
        {
            get { return _profileOption; }
            set { _profileOption = value; NotifyPropertyChanged(); }
        }

        public ModelTemplateViewModel SelectedModelTemplate
        {
            get { return _selectedModelTemplate; }
            set { _selectedModelTemplate = value; NotifyPropertyChanged(); }
        }

        public HardwareProfileQualityType SelectedModelQuality
        {
            get { return _selectedModelQuality; }
            set { _selectedModelQuality = value; NotifyPropertyChanged(); OnGenerationModeChanged(); }
        }

        public HardwareProfileAspectType SelectedResolution
        {
            get { return _selectedResolution; }
            set { _selectedResolution = value; NotifyPropertyChanged(); }
        }

        public bool IsNegativePromptEnabled
        {
            get { return _isNegativePromptEnabled; }
            set { _isNegativePromptEnabled = value; NotifyPropertyChanged(); }
        }

        public bool IsPromptEnhanceEnabled
        {
            get { return _isPromptEnhanceEnabled; }
            set { _isPromptEnhanceEnabled = value; NotifyPropertyChanged(); }
        }

        public bool IsRandomSeedEnabled
        {
            get { return _isRandomSeedEnabled; }
            set { _isRandomSeedEnabled = value; NotifyPropertyChanged(); }
        }

        public bool IsNPUStableDiffusionVisible
        {
            get { return _isNPUStableDiffusionVisible; }
            set { _isNPUStableDiffusionVisible = value; NotifyPropertyChanged(); }
        }

        public bool IsNPUStableDiffusionEnabled
        {
            get { return _isNPUStableDiffusionEnabled; }
            set
            {
                _isNPUStableDiffusionEnabled = value;
                SelectResolution(HardwareProfileAspectType.Default);
                NotifyPropertyChanged();
            }
        }

        public bool IsQualityBoostSupported
        {
            get { return _isQualityBoostSupported; }
            set { _isQualityBoostSupported = value; NotifyPropertyChanged(); }
        }

        public bool IsQualityBoostEnabled
        {
            get { return _isQualityBoostEnabled; }
            set { _isQualityBoostEnabled = value; NotifyPropertyChanged(); SetSteps(); }
        }

        public int SelectedVideoLength
        {
            get { return _selectedVideoLength; }
            set { _selectedVideoLength = value; NotifyPropertyChanged(); }
        }

        public DefaultSetting SelectedCoherency
        {
            get { return _selectedCoherency; }
            set { _selectedCoherency = value; NotifyPropertyChanged(); }
        }

        public DefaultSetting SelectedVolatility
        {
            get { return _selectedVolatility; }
            set { _selectedVolatility = value; NotifyPropertyChanged(); }
        }

        public Dock ResultListPosition
        {
            get { return _resultListPosition; }
            set { _resultListPosition = value; NotifyPropertyChanged(); }
        }

        public int ResultListRows
        {
            get { return _resultListRows; }
            set { _resultListRows = value; NotifyPropertyChanged(); }
        }

        public int ResultListColumns
        {
            get { return _resultListColumns; }
            set { _resultListColumns = value; NotifyPropertyChanged(); }
        }


        protected override Task OnSettingsChanged()
        {
            _generateSettings = Settings.EZModeProfile.Generate;
            BatchOptions.ValueTo = 4;
            PromptOptions.Prompt = _generateSettings.DemoPrompt;
            SetResultLayout();
            return base.OnSettingsChanged();
        }


        protected override Task OnDefaultExecutionDeviceChanged()
        {
            var hardwareProfile = DeviceService.GetHardwareProfile();
            _hardwareProfile = hardwareProfile.Generate;
            SelectedModelQuality = _hardwareProfile.DefaultQuality;

            Logger.LogInformation($"[GenerateView] [OnDefaultExecutionDeviceChanged] - Hardware profile selected, Profile: {hardwareProfile.Name}");
            return base.OnDefaultExecutionDeviceChanged();
        }


        protected override void OnGenerationModeChanged()
        {
            BatchOptions.IsRealtimeEnabled = false;
            _profileGroup = GetHardwareProfileOption(_selectedModelQuality);
            ProfileOption = IsVideoGenerationMode
                ? _profileGroup.VideoProfile
                : _profileGroup.ImageProfile;

            SelectResolution(ProfileOption.Aspect);
            SelectedModelTemplate = Settings.Templates.FirstOrDefault(x => x.Id == ProfileOption.ModelId);
            SetSteps();
            base.OnGenerationModeChanged();
        }


        protected override async Task<GenerateOptions> GetGenerateImageOptionsAsync(CancellationToken cancellationToken)
        {
            var promptOptions = await base.GetGenerateImageOptionsAsync(cancellationToken);
            promptOptions.Diffuser = DiffuserType.TextToImage;
            if (_isPromptEnhanceEnabled)
            {
                promptOptions.Prompt += _generateSettings.ImagePrompt;
                promptOptions.NegativePrompt += _generateSettings.ImageNegativePrompt;
            }
            return promptOptions;
        }


        protected override Task<GenerateOptions> GetGenerateVideoOptionsAsync(CancellationToken cancellationToken)
        {
            var generateOptions = PromptOptionsModel.ToGenerateVideoOptions(PromptOptions, SchedulerOptions, MemoryInfo);
            generateOptions.MotionFrames = SelectedVideoLength;
            generateOptions.MotionContextOverlap = GetMotionContextOverlap();
            generateOptions.MotionNoiseContext = GetMotionNoiseContext();
            generateOptions.Diffuser = DiffuserType.TextToVideo;
            if (_isPromptEnhanceEnabled)
            {
                generateOptions.Prompt += _generateSettings.VideoPrompt;
                generateOptions.NegativePrompt += _generateSettings.VideoNegativePrompt;
                for (int i = 0; i < generateOptions.Prompts.Count; i++)
                {
                    generateOptions.Prompts[i] += _generateSettings.VideoPrompt;
                }
            }
            return Task.FromResult(generateOptions);
        }


        protected override async Task GenerateImage()
        {
            try
            {

                if (await ModeratorService.ContainsExplicitContentAsync(PromptOptions.Prompt))
                    throw new OperationCanceledException();

                // Check if model is installed
                if (!SelectedModelTemplate.IsInstalled)
                {
                    var downloadModelDialog = DialogService.GetDialog<ModelDownloadDialog>();
                    if (!await downloadModelDialog.ShowDialogAsync(_selectedModelTemplate))
                        throw new OperationCanceledException();
                }

                ResultImage = null;
                IsGenerating = true;
                SetResultLayout();
                using (CancelationTokenSource = new CancellationTokenSource())
                {
                    // Select & Load Model
                    SelectedBaseModel = Settings.StableDiffusionModelSets.FirstOrDefault(x => x.Id == _selectedModelTemplate.Id);

                    // Load Pipeline
                    await LoadPipelineAsync();

                    // LoadPipeline will reset Scheduler Options to default
                    // so reset the Resolution/Aspect ratio
                    SelectResolution(_selectedResolution);
                    SetSteps();

                    if (_isRandomSeedEnabled)
                        RandomizeSeed();

                    // Generate
                    SetDisplaySize();
                    StartStatistics();
                    SchedulerOptions.Seed = _currentSeed;
                    if (BatchOptions.IsRealtimeEnabled)
                    {
                        var imageCount = 1;
                        await foreach (var resultImage in GenerateImageRealtimeAsync(CancelationTokenSource.Token))
                        {
                            if (resultImage is null)
                                continue;

                            ResultImage = resultImage;
                            ImageResults.Insert(0, resultImage);
                            if (ImageResults.Count > BatchOptions.ValueTo)
                                ImageResults.RemoveAt(ImageResults.Count - 1);

                            if (BatchOptions.IsRealtimeEnabled && IsRandomSeedEnabled)
                                SchedulerOptions.Seed = 0;

                            Logger.LogInformation($"[GenerateView] [Generate] - Realtime Image Count: {imageCount}");
                            imageCount++;

                            Statistics.Reset();
                        }
                    }
                    else if (BatchOptions.IsAutomationEnabled)
                    {
                        var index = 0;
                        await foreach (var resultImage in GenerateImageBatchAsync(CancelationTokenSource.Token))
                        {
                            if (resultImage is null)
                                continue;

                            ResultImage = resultImage;
                            ImageResults[index] = resultImage;
                            index++;
                            if (index < BatchOptions.ValueTo)
                                Statistics.Reset();
                        }
                    }
                    else
                    {
                        var resultImage = await GenerateImageResultAsync(CancelationTokenSource.Token);
                        ResultImage = resultImage;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("[GenerateView] [Generate] - Generate was canceled");
            }
            catch (UnrecoverableException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("[GenerateView] [Generate] - Error during Generate\n{ex}", ex);
                await App.UIInvokeAsync(() => DialogService.ShowErrorMessageAsync("Generate Error", ex.Message));
            }
            StopStatistics();
            Reset();
        }


        protected override async Task GenerateVideo()
        {
            try
            {
                if (await ModeratorService.ContainsExplicitContentAsync(PromptOptions.Prompt))
                    throw new OperationCanceledException();

                // Check if model is installed
                if (!SelectedModelTemplate.IsInstalled)
                {
                    var downloadModelDialog = DialogService.GetDialog<ModelDownloadDialog>();
                    if (!await downloadModelDialog.ShowDialogAsync(_selectedModelTemplate))
                        throw new OperationCanceledException();
                }

                IsGenerating = true;
                ResultVideo = null;
                SetResultLayout();
                using (CancelationTokenSource = new CancellationTokenSource())
                {
                    var variant = default(string);
                    if (CurrentPipeline?.IsLoaded == true)
                    {
                        if (SelectedVariant != variant)
                            await UnloadPipelineAsync();
                    }


                    // Select & Load Model
                    SelectedBaseModel = Settings.StableDiffusionModelSets.FirstOrDefault(x => x.Id == _selectedModelTemplate.Id);
                    SelectedVariant = variant;

                    // Load Pipeline
                    await LoadPipelineAsync();

                    // LoadPipeline will reset Scheduler Options to default
                    // so reset the Resolution/Aspect ratio
                    SelectResolution(_selectedResolution);

                    // InferenceSteps
                    SetSteps();

                    if (_isRandomSeedEnabled)
                        RandomizeSeed();

                    // Generate
                    SetDisplaySize();
                    StartStatistics();
                    SchedulerOptions.Seed = _currentSeed;
                    if (BatchOptions.IsAutomationEnabled)
                    {
                        var index = 0;
                        await foreach (var resultVideo in GenerateVideoBatchAsync(CancelationTokenSource.Token))
                        {
                            if (resultVideo is null)
                                continue;

                            ResultVideo = resultVideo;
                            VideoResults[index] = resultVideo;
                            index++;

                            if (index < BatchOptions.ValueTo)
                                Statistics.Reset();
                        }
                    }
                    else
                    {
                        var resultImage = await GenerateVideoResultAsync(CancelationTokenSource.Token);
                        ResultVideo = resultImage;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("[GenerateView] [Generate] - Generate was canceled");
            }
            catch (UnrecoverableException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("[GenerateView] [Generate] - Error during Generate\n{ex}", ex);
                await App.UIInvokeAsync(() => DialogService.ShowErrorMessageAsync("Generate Error", ex.Message));
            }
            StopStatistics();
            Reset();
        }


        protected override bool CanExecuteGenerate()
        {
            return !IsGenerating && !string.IsNullOrEmpty(PromptOptions.Prompt);
        }


        protected override async Task ClearHistory()
        {
            await base.ClearHistory();
            SetResultLayout();
        }


        protected override bool CanExecuteClearHistory()
        {
            return ResultImage is not null || ResultVideo is not null;
        }


        private int RandomizeSeed()
        {
            _currentSeed = Random.Shared.Next();
            return _currentSeed;
        }


        private Task ShowImagePreview(ImageResult source)
        {
            ResultImage = source;
            return Task.CompletedTask;
        }


        private Task ShowVideoPreview(VideoResultModel source)
        {
            ResultVideo = source;
            return Task.CompletedTask;
        }


        private void SetSteps()
        {
            var multiplier = _isQualityBoostEnabled ? 2 : 1;
            SchedulerOptions.InferenceSteps = _selectedModelQuality switch
            {
                HardwareProfileQualityType.Fast => ProfileOption.Steps[0],
                HardwareProfileQualityType.Balanced => ProfileOption.Steps[1],
                HardwareProfileQualityType.Quality => ProfileOption.Steps[2] * multiplier,
                _ => 8
            };
        }


        private HardwareProfileGroup GetHardwareProfileOption(HardwareProfileQualityType quality)
        {
            return quality switch
            {
                HardwareProfileQualityType.Fast => _hardwareProfile.Fast,
                HardwareProfileQualityType.Balanced => _hardwareProfile.Balanced,
                HardwareProfileQualityType.Quality => _hardwareProfile.Quality,
                _ => _hardwareProfile.Fast
            };
        }


        private void SelectResolution(HardwareProfileAspectType resolution)
        {
            SelectedResolution = resolution;
            if (resolution == HardwareProfileAspectType.Default)
            {
                SchedulerOptions.Width = ProfileOption.Default.Width;
                SchedulerOptions.Height = ProfileOption.Default.Height;
            }
            else if (resolution == HardwareProfileAspectType.Landscape)
            {
                SchedulerOptions.Width = ProfileOption.Landscape.Width;
                SchedulerOptions.Height = ProfileOption.Landscape.Height;
            }
            else if (resolution == HardwareProfileAspectType.Portrait)
            {
                SchedulerOptions.Width = ProfileOption.Portrait.Width;
                SchedulerOptions.Height = ProfileOption.Portrait.Height;
            }
        }


        private void SetDisplaySize()
        {
            var isDefaultHeight = Math.Round(App.CurrentWindow.Height) == App.CurrentWindow.MinHeight;
            if (!isDefaultHeight)
                return;

            if (_selectedResolution == HardwareProfileAspectType.Default)
            {
                ResultListPosition = Dock.Bottom;
                ResultListColumns = (int)BatchOptions.ValueTo;
                ResultListRows = 0;
                App.CurrentWindow.AnimateWidth(840);
            }
            else if (_selectedResolution == HardwareProfileAspectType.Landscape)
            {
                ResultListPosition = Dock.Bottom;
                ResultListColumns = (int)BatchOptions.ValueTo;
                ResultListRows = 0;
                App.CurrentWindow.AnimateWidth(1102);
            }
            else if (_selectedResolution == HardwareProfileAspectType.Portrait)
            {
                ResultListPosition = Dock.Right;
                ResultListColumns = 0;
                ResultListRows = (int)BatchOptions.ValueTo;
                App.CurrentWindow.AnimateWidth(840);
            }
        }


        private void SetResultLayout()
        {
            ImageResults.Clear();
            VideoResults.Clear();
            if (BatchOptions.ValueTo > 1)
            {
                for (int i = 0; i < BatchOptions.ValueTo; i++)
                {
                    ImageResults.Add(new ImageResult { Image = Utils.CreateEmptyBitmapImage(SchedulerOptions.Width, SchedulerOptions.Height) });
                    VideoResults.Add(new VideoResultModel { Thumbnail = Utils.CreateEmptyBitmapImage(SchedulerOptions.Width, SchedulerOptions.Height) });
                }
            }

            if (BatchOptions.IsRealtimeEnabled)
                return;

            BatchOptions.IsAutomationEnabled = BatchOptions.ValueTo > 1;
        }


        private int GetMotionContextOverlap()
        {
            return SelectedCoherency switch
            {
                DefaultSetting.Minimum => 1,
                DefaultSetting.Medium => 3,
                DefaultSetting.Maximum => 8,
                _ => 0
            };
        }


        private int GetMotionNoiseContext()
        {
            if (SelectedVideoLength == 32)
            {
                return SelectedVolatility switch
                {
                    DefaultSetting.Minimum => 16,
                    DefaultSetting.Medium => 32,
                    DefaultSetting.Maximum => 32,
                    _ => 16
                };
            }
            else if (SelectedVideoLength == 48)
            {
                return SelectedVolatility switch
                {
                    DefaultSetting.Minimum => 16,
                    DefaultSetting.Medium => 24,
                    DefaultSetting.Maximum => 48,
                    _ => 16
                };
            }
            return 16;
        }


        /// <summary>
        /// Updates the progress Image.
        /// </summary>
        /// <returns>Task.</returns>
        protected override Task UpdateProgressImage(DiffusionProgress progress)
        {
            var index = progress.BatchValue - 1;
            if (index >= 0 && index < ImageResults.Count)
                ImageResults[progress.BatchValue - 1].PreviewImage = PreviewResult;

            return Task.CompletedTask;
        }


        private async Task InfoDialog()
        {
            var infoDialog = DialogService.GetDialog<Dialogs.EZMode.InformationDialog>();
            await infoDialog.ShowDialogAsync();
        }


        private async Task SettingsDialog()
        {
            var settingsDialog = DialogService.GetDialog<Dialogs.EZMode.SettingsDialog>();
            if (await settingsDialog.ShowDialogAsync(_isNegativePromptEnabled, _isPromptEnhanceEnabled))
            {
                IsPromptEnhanceEnabled = settingsDialog.IsPromptEnhanceEnabled;
                IsNegativePromptEnabled = settingsDialog.IsNegativePromptEnabled;
            }
        }


        private Task ShowNormalMode()
        {
            SwitchUIModeCommand.Execute(UIModeType.Normal);
            return Task.CompletedTask;
        }

    }

    public enum DefaultSetting
    {
        Minimum = 0,
        Medium = 1,
        Maximum = 2
    }
}