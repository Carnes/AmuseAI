using Amuse.UI.Commands;
using Amuse.UI.Dialogs;
using Amuse.UI.Enums;
using Amuse.UI.Helpers;
using Amuse.UI.Models;
using Amuse.UI.Models.StableDiffusion;
using Amuse.UI.Services;
using OnnxStack.StableDiffusion.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Amuse.UI.UserControls
{
    /// <summary>
    /// Interaction logic for StableDiffusionInputControlBase.xaml
    /// </summary>
    public partial class StableDiffusionInputControlBase : UserControl, INotifyPropertyChanged
    {
        private readonly IDialogService _dialogService;

        private ICollectionView _promptCollectionView;
        private ICollectionView _promptNegativeCollectionView;
        private ICollectionView _promptSnippitCollectionView;
        private StableDiffusionSchedulerDefaults _schedulerDefaults;
        private SchedulerModel _selectedScheduler;
        private List<ResolutionOption> _resolutionOptions;
        private ResolutionOption _selectedResolution;
        private MemoryMode _memoryMode;
        private bool _isMemoryModeMinimized = true;
        private int _promptTokenLimit;

        /// <summary>Initializes a new instance of the <see cref="StableDiffusionInputControlBase" /> class.</summary>
        public StableDiffusionInputControlBase()
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
                _dialogService = App.GetService<IDialogService>();

            ValidSizes = new List<int>(Enumerable.Range(1, 48).Select(x => 64 * x));
            NewSeedCommand = new RelayCommand(NewSeed);
            RandomSeedCommand = new RelayCommand(RandomSeed);
            ResetSchedulerOptionsCommand = new AsyncRelayCommand(ResetOptions);
            SavePromptCommand = new AsyncRelayCommand<PromptInputType>(SavePrompt);
            AppendPromptCommand = new AsyncRelayCommand<PromptInputModel>(AppendPrompt);
            AppendPromptNegativeCommand = new AsyncRelayCommand<PromptInputModel>(AppendPromptNegative);
            Schedulers = new ObservableCollection<SchedulerModel>();
        }

        public static readonly DependencyProperty SettingsProperty =
            DependencyProperty.Register(nameof(Settings), typeof(AmuseSettings), typeof(StableDiffusionInputControlBase), new PropertyMetadata<StableDiffusionInputControlBase>(x => x.OnSettingsChanged()));

        public static readonly DependencyProperty PromptOptionsProperty =
            DependencyProperty.Register(nameof(PromptOptions), typeof(PromptOptionsModel), typeof(StableDiffusionInputControlBase));

        public static readonly DependencyProperty SchedulerOptionsProperty =
            DependencyProperty.Register(nameof(SchedulerOptions), typeof(SchedulerOptionsModel), typeof(StableDiffusionInputControlBase));

        public static readonly DependencyProperty BatchOptionsProperty =
            DependencyProperty.Register(nameof(BatchOptions), typeof(BatchOptionsModel), typeof(StableDiffusionInputControlBase));

        public static readonly DependencyProperty CurrentPipelineProperty =
            DependencyProperty.Register(nameof(CurrentPipeline), typeof(StableDiffusionPipelineModel), typeof(StableDiffusionInputControlBase), new PropertyMetadata<StableDiffusionInputControlBase, StableDiffusionPipelineModel>((x, o, n) => x.OnCurrentPipelineChanged(o, n)));

        public static readonly DependencyProperty IsGeneratingProperty =
            DependencyProperty.Register(nameof(IsGenerating), typeof(bool), typeof(StableDiffusionInputControlBase));

        public static readonly DependencyProperty DiffuserTypeProperty =
            DependencyProperty.Register(nameof(DiffuserType), typeof(DiffuserType), typeof(StableDiffusionInputControlBase));

        public static readonly DependencyProperty IsVideoControlsEnabledProperty =
            DependencyProperty.Register(nameof(IsVideoControlsEnabled), typeof(bool), typeof(StableDiffusionInputControlBase));

        public static readonly DependencyProperty MemoryInfoProperty =
            DependencyProperty.Register(nameof(MemoryInfo), typeof(MemoryInfoModel), typeof(StableDiffusionInputControlBase));

        public List<int> ValidSizes { get; }
        public ObservableCollection<SchedulerModel> Schedulers { get; }
        public ICommand NewSeedCommand { get; }
        public ICommand RandomSeedCommand { get; }
        public AsyncRelayCommand ResetSchedulerOptionsCommand { get; }
        public AsyncRelayCommand<PromptInputType> SavePromptCommand { get; }
        public AsyncRelayCommand<PromptInputModel> AppendPromptCommand { get; }
        public AsyncRelayCommand<PromptInputModel> AppendPromptNegativeCommand { get; }

        /// <summary>
        /// Gets or sets the settings.
        /// </summary>
        public AmuseSettings Settings
        {
            get { return (AmuseSettings)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }

        /// <summary>
        /// Gets or sets the PromptOptions.
        /// </summary>
        public PromptOptionsModel PromptOptions
        {
            get { return (PromptOptionsModel)GetValue(PromptOptionsProperty); }
            set { SetValue(PromptOptionsProperty, value); }
        }

        /// <summary>
        /// Gets or sets the scheduler options.
        /// </summary>
        public SchedulerOptionsModel SchedulerOptions
        {
            get { return (SchedulerOptionsModel)GetValue(SchedulerOptionsProperty); }
            set { SetValue(SchedulerOptionsProperty, value); }
        }

        /// <summary>
        /// Gets or sets the batch options.
        /// </summary>
        public BatchOptionsModel BatchOptions
        {
            get { return (BatchOptionsModel)GetValue(BatchOptionsProperty); }
            set { SetValue(BatchOptionsProperty, value); }
        }

        /// <summary>
        /// Gets or sets the current pipeline.
        /// </summary>
        public StableDiffusionPipelineModel CurrentPipeline
        {
            get { return (StableDiffusionPipelineModel)GetValue(CurrentPipelineProperty); }
            set { SetValue(CurrentPipelineProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is generating.
        /// </summary>
        public bool IsGenerating
        {
            get { return (bool)GetValue(IsGeneratingProperty); }
            set { SetValue(IsGeneratingProperty, value); }
        }

        /// <summary>
        /// Gets or sets the type of the diffuser.
        /// </summary>
        public DiffuserType DiffuserType
        {
            get { return (DiffuserType)GetValue(DiffuserTypeProperty); }
            set { SetValue(DiffuserTypeProperty, value); }
        }

        /// <summary>
        /// Gets or sets the scheduler defaults.
        /// </summary>
        public StableDiffusionSchedulerDefaults SchedulerDefaults
        {
            get { return _schedulerDefaults; }
            set { _schedulerDefaults = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is video controls enabled.
        public bool IsVideoControlsEnabled
        {
            get { return (bool)GetValue(IsVideoControlsEnabledProperty); }
            set { SetValue(IsVideoControlsEnabledProperty, value); }
        }

        public ICollectionView PromptCollectionView
        {
            get { return _promptCollectionView; }
            set { _promptCollectionView = value; NotifyPropertyChanged(); }
        }

        public ICollectionView PromptNegativeCollectionView
        {
            get { return _promptNegativeCollectionView; }
            set { _promptNegativeCollectionView = value; NotifyPropertyChanged(); }
        }

        public ICollectionView PromptSnippitCollectionView
        {
            get { return _promptSnippitCollectionView; }
            set { _promptSnippitCollectionView = value; NotifyPropertyChanged(); }
        }

        public SchedulerModel SelectedScheduler
        {
            get { return _selectedScheduler; }
            set
            {
                _selectedScheduler = value;
                if (_selectedScheduler is not null)
                {
                    SchedulerOptions.SchedulerType = _selectedScheduler.Scheduler;
                    SchedulerOptions.UseKarrasSigmas = _selectedScheduler.IsKarras;
                }
                NotifyPropertyChanged();
            }
        }

        public List<ResolutionOption> ResolutionOptions
        {
            get { return _resolutionOptions; }
            set { _resolutionOptions = value; NotifyPropertyChanged(); }
        }


        public ResolutionOption SelectedResolution
        {
            get { return _selectedResolution; }
            set
            {
                _selectedResolution = value;
                if (SchedulerOptions != null && _selectedResolution != null)
                {
                    SchedulerOptions.Width = _selectedResolution.Width;
                    SchedulerOptions.Height = _selectedResolution.Height;
                }
                NotifyPropertyChanged();
            }
        }


        public MemoryInfoModel MemoryInfo
        {
            get { return (MemoryInfoModel)GetValue(MemoryInfoProperty); }
            set { SetValue(MemoryInfoProperty, value); }
        }

        public MemoryMode MemoryMode
        {
            get { return _memoryMode; }
            set
            {
                _memoryMode = value;
                MemoryInfo?.Update(_memoryMode, DiffuserType, SchedulerOptions?.Strength ?? 0);
                if (_memoryMode == MemoryMode.Custom && IsMemoryModeMinimized)
                    IsMemoryModeMinimized = false;

                NotifyPropertyChanged();
            }
        }

        public bool IsMemoryModeMinimized
        {
            get { return _isMemoryModeMinimized; }
            set { _isMemoryModeMinimized = value; NotifyPropertyChanged(); }
        }

        public int PromptTokenLimit
        {
            get { return _promptTokenLimit; }
            set { _promptTokenLimit = value; NotifyPropertyChanged(); }
        }


        /// <summary>
        /// Called when Settings changed.
        /// </summary>
        /// <returns></returns>
        private Task OnSettingsChanged()
        {
            PromptCollectionView = new ListCollectionView(Settings.Prompts);
            PromptCollectionView.Filter = (obj) =>
            {
                if (obj is not PromptInputModel viewModel)
                    return false;
                return viewModel.Type == PromptInputType.Positive;
            };
            PromptNegativeCollectionView = new ListCollectionView(Settings.Prompts);
            PromptNegativeCollectionView.Filter = (obj) =>
            {
                if (obj is not PromptInputModel viewModel)
                    return false;
                return viewModel.Type == PromptInputType.Negative;
            };
            PromptSnippitCollectionView = new ListCollectionView(Settings.Prompts);
            PromptSnippitCollectionView.Filter = (obj) =>
            {
                if (obj is not PromptInputModel viewModel)
                    return false;
                return viewModel.Type == PromptInputType.Snippit;
            };
            return Task.CompletedTask;
        }


        /// <summary>
        /// Resets the options.
        /// </summary>
        /// <returns></returns>
        private Task ResetOptions()
        {
            return ResetSchedulerOptions(false, false);
        }


        /// <summary>
        /// Resets the scheduler options.
        /// </summary>
        /// <param name="preserveOptions">if set to <c>true</c> [preserve options].</param>
        /// <returns></returns>
        private Task ResetSchedulerOptions(bool preserveOptions, bool preserveSize)
        {
            var defaultSchedulerOptions = CurrentPipeline.DefaultSchedulerOptions;
            var schedulerOptions = defaultSchedulerOptions with
            {
                Seed = SchedulerOptions is null ? 0 : SchedulerOptions.Seed,
                GuidanceScale = SchedulerDefaults.Guidance,
                GuidanceScale2 = SchedulerDefaults.Guidance2,
                InferenceSteps = SchedulerDefaults.Steps,
                SchedulerType = SchedulerDefaults.SchedulerType,
                Timesteps = SchedulerDefaults.Timesteps,
                BetaStart = SchedulerDefaults.BetaStart,
                BetaEnd = SchedulerDefaults.BetaEnd,
                BetaSchedule = SchedulerDefaults.BetaSchedule,
                TimestepSpacing = SchedulerDefaults.TimestepSpacing,
                PredictionType = SchedulerDefaults.PredictionType,
                UseKarrasSigmas = SchedulerDefaults.UseKarrasSigmas
            };

            if (preserveSize)
            {
                schedulerOptions.Width = SchedulerOptions is null ? schedulerOptions.Width : SchedulerOptions.Width;
                schedulerOptions.Height = SchedulerOptions is null ? schedulerOptions.Height : SchedulerOptions.Height;
            }

            if (preserveOptions)
            {

                schedulerOptions.GuidanceScale = SchedulerOptions is null ? SchedulerDefaults.Guidance : SchedulerOptions.GuidanceScale;
                schedulerOptions.InferenceSteps = SchedulerOptions is null ? SchedulerDefaults.Steps : SchedulerOptions.InferenceSteps;
                schedulerOptions.SchedulerType = SchedulerOptions is null ? SchedulerDefaults.SchedulerType : SchedulerOptions.SchedulerType;
                //schedulerOptions.MotionFrames = SchedulerOptions is null ? CurrentPipeline.ContextSize : SchedulerOptions.MotionFrames;
                //schedulerOptions.MotionNoiseContext = SchedulerOptions is null ? CurrentPipeline.ContextSize : SchedulerOptions.MotionNoiseContext;
                //schedulerOptions.MotionStrides = SchedulerOptions is null ? 0 : SchedulerOptions.MotionStrides;
                //schedulerOptions.MotionContextOverlap = SchedulerOptions is null ? 3 : SchedulerOptions.MotionContextOverlap;
            }

            SchedulerOptions = SchedulerOptionsModel.FromSchedulerOptions(schedulerOptions);
            SchedulerOptions.Strength = CurrentPipeline.IsControlNetEnabled ? 1 : 0.75f;
            SelectedScheduler = Schedulers.FirstOrDefault(x => x.Scheduler == SchedulerOptions.SchedulerType && x.IsKarras == SchedulerOptions.UseKarrasSigmas) ?? SelectedScheduler;
            return Task.CompletedTask;
        }


        /// <summary>
        /// Creates new seed.
        /// </summary>
        private void NewSeed()
        {
            SchedulerOptions.Seed = Random.Shared.Next();
        }


        /// <summary>
        /// Sets seed to 0 (Random)
        /// </summary>
        private void RandomSeed()
        {
            SchedulerOptions.Seed = 0;
        }


        protected virtual List<ResolutionOption> GetResolutions(PipelineType pipelineType, ModelType modelType)
        {
            if ((pipelineType == PipelineType.StableDiffusionXL && modelType != ModelType.Turbo)
             || pipelineType == PipelineType.StableCascade
             || pipelineType == PipelineType.StableDiffusion3
             || pipelineType == PipelineType.Flux)
                return
                [
                    new ResolutionOption(640 , 1536, ResolutionType.Vertical, "Portrait"),
                    new ResolutionOption(768 , 1344, ResolutionType.Vertical, "Portrait"),
                    new ResolutionOption(832 , 1280, ResolutionType.Vertical, "Portrait"),
                    new ResolutionOption(896 , 1152, ResolutionType.Vertical, "Portrait"),

                    new ResolutionOption(512, 512, ResolutionType.Square, "Square"),
                    new ResolutionOption(768, 768, ResolutionType.Square, "Square"),
                    new ResolutionOption(1024, 1024, ResolutionType.Square, "Square"),

                    new ResolutionOption(1152, 896, ResolutionType.Horizontal, "Landscape"),
                    new ResolutionOption(1280, 832, ResolutionType.Horizontal, "Landscape"),
                    new ResolutionOption(1344, 768, ResolutionType.Horizontal, "Landscape"),
                    new ResolutionOption(1536, 640, ResolutionType.Horizontal, "Landscape")
                ];

            return
            [
                new ResolutionOption(576 ,896, ResolutionType.Vertical, "Portrait"),
                new ResolutionOption(512, 768, ResolutionType.Vertical, "Portrait"),
                new ResolutionOption(448, 640, ResolutionType.Vertical, "Portrait"),

                new ResolutionOption(512, 512, ResolutionType.Square, "Square"),
                new ResolutionOption(768, 768, ResolutionType.Square, "Square"),

                new ResolutionOption(640, 448, ResolutionType.Horizontal, "Landscape"),
                new ResolutionOption(768, 512, ResolutionType.Horizontal, "Landscape"),
                new ResolutionOption(896, 576, ResolutionType.Horizontal, "Landscape"),
            ];
        }


        /// <summary>
        /// Called when CurrentPipeline changed.
        /// </summary>
        /// <param name="oldPipeline">The old pipeline.</param>
        /// <param name="newPipeline">The new pipeline.</param>
        /// <returns></returns>
        protected virtual async Task OnCurrentPipelineChanged(StableDiffusionPipelineModel oldPipeline, StableDiffusionPipelineModel newPipeline)
        {
            if (newPipeline is not null)
            {
                PromptOptions.OptimizationType = newPipeline.OptimizationType;
                SchedulerDefaults = Settings.Templates
                    .Where(x => x.Id == newPipeline.BaseModel.Id)
                    .Select(x => x.StableDiffusionTemplate.SchedulerDefaults)
                    .FirstOrDefault(new StableDiffusionSchedulerDefaults());

                SetSchedulers(newPipeline);
                var preserveOptions = oldPipeline is not null && oldPipeline.PipelineType == newPipeline.PipelineType;
                var preserveSize = oldPipeline is not null && oldPipeline.SampleSize == newPipeline.SampleSize;
                var preserveTemplate = oldPipeline?.BaseModel.Template.Template == newPipeline.BaseModel.Template.Template
                                    && oldPipeline?.BaseModel.Template.StableDiffusionTemplate.ModelType == newPipeline.BaseModel.Template.StableDiffusionTemplate.ModelType;
                await ResetSchedulerOptions(preserveOptions && preserveTemplate, preserveSize);

                MemoryInfo?.Update(MemoryMode, DiffuserType, SchedulerOptions.Strength);

                PromptTokenLimit = GetPromptTokenLimit(newPipeline);
            }
        }


        private void SetSchedulers(StableDiffusionPipelineModel pipeline)
        {
            Schedulers.Clear();
            foreach (var scheduler in pipeline.SupportedSchedulers)
            {
                Schedulers.Add(new SchedulerModel(scheduler, false));
                if (IsKarrasScheduler(scheduler))
                    Schedulers.Add(new SchedulerModel(scheduler, true));
            }

            SelectedScheduler = Schedulers.FirstOrDefault(x => x.Scheduler == pipeline.DefaultSchedulerOptions.SchedulerType);
        }


        private bool IsKarrasScheduler(SchedulerType schedulerType)
        {
            return schedulerType == SchedulerType.LMS
                || schedulerType == SchedulerType.Euler
                || schedulerType == SchedulerType.EulerAncestral
                || schedulerType == SchedulerType.KDPM2
                || schedulerType == SchedulerType.KDPM2Ancestral;
        }


        private int GetPromptTokenLimit(StableDiffusionPipelineModel pipeline)
        {
            return 0;
        }


        private Task AppendPrompt(PromptInputModel promptInput)
        {
            if (promptInput.Type == PromptInputType.Positive)
                PromptOptions.Prompt = promptInput.Prompt;
            else if (promptInput.Type == PromptInputType.Snippit)
                PromptOptions.Prompt += promptInput.Prompt;

            return Task.CompletedTask;
        }


        private Task AppendPromptNegative(PromptInputModel promptInput)
        {
            if (promptInput.Type == PromptInputType.Negative)
                PromptOptions.NegativePrompt = promptInput.Prompt;
            else if (promptInput.Type == PromptInputType.Snippit)
                PromptOptions.NegativePrompt += promptInput.Prompt;

            return Task.CompletedTask;
        }


        private async Task SavePrompt(PromptInputType promptInputType)
        {
            var promptText = promptInputType == PromptInputType.Positive
                ? PromptOptions.Prompt
                : PromptOptions.NegativePrompt;
            var promptDialog = _dialogService.GetDialog<AddPromptInputDialog>();
            if (await promptDialog.ShowDialogAsync(promptText, promptInputType))
                await Settings.SaveAsync();
        }


        private void SliderStrength_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if ((e.OldValue == 1 && e.NewValue < 1) || (e.OldValue < 1 && e.NewValue == 1))
            {
                MemoryInfo?.Update(MemoryMode, DiffuserType, SchedulerOptions?.Strength ?? 1);
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
