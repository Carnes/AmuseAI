using Amuse.UI.Commands;
using Amuse.UI.Core.Models;
using Amuse.UI.Core.Services;
using Amuse.UI.Dialogs;
using Amuse.UI.Models;
using Amuse.UI.Models.StableDiffusion;
using Microsoft.Extensions.Logging;
using OnnxStack.Core;
using OnnxStack.Core.Image;
using OnnxStack.FeatureExtractor.Common;
using OnnxStack.ImageUpscaler.Common;
using OnnxStack.StableDiffusion.Common;
using OnnxStack.StableDiffusion.Config;
using OnnxStack.StableDiffusion.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Amuse.UI.Views
{
    public class StableDiffusionImageViewBase : StableDiffusionViewBase, INavigatable
    {
        private PromptOptionsModel _promptOptionsModel;
        private SchedulerOptionsModel _schedulerOptions;
        private BatchOptionsModel _batchOptions;
        private ImageResult _resultImage;
        private int _selectedTabIndex;
        private bool _isHistoryView;
        private bool _realtimeHasChanged;
        private ImageInput _inputImage;
        private OnnxImage _inputImageCached;
        private OnnxImage _inputControlImageCached;
        private BitmapSource _previewResult;
        private ImageInput _sourceImage;
        private IJobQueueService _jobQueueService;

        public StableDiffusionImageViewBase()
        {
            CancelCommand = new AsyncRelayCommand(Cancel, CanExecuteCancel);
            GenerateCommand = new AsyncRelayCommand(Generate, CanExecuteGenerate);
            SaveHistoryCommand = new AsyncRelayCommand(SaveHistory, CanExecuteSaveHistory);
            ClearHistoryCommand = new AsyncRelayCommand(ClearHistory, CanExecuteClearHistory);
            SaveImageCommand = new AsyncRelayCommand<ImageResult>(SaveImage);
            CopyImageCommand = new AsyncRelayCommand<ImageResult>(CopyImage);
            RemoveImageCommand = new AsyncRelayCommand<ImageResult>(RemoveImage);
            PreviewImageCommand = new AsyncRelayCommand<ImageResult>(PreviewImage);
            UpdateSeedCommand = new AsyncRelayCommand<int>(UpdateSeed);
            PromptOptions = new PromptOptionsModel();
            SchedulerOptions = CreateSchedulerOptionsFromDefaults();
            BatchOptions = new BatchOptionsModel();
            ImageResults = new ObservableCollection<ImageResult>();

            // Subscribe to API job completions for history integration
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                _jobQueueService = App.GetService<IJobQueueService>();
                if (_jobQueueService != null)
                {
                    _jobQueueService.JobCompleted += OnApiJobCompleted;
                }
            }
        }

        /// <summary>
        /// Creates a SchedulerOptionsModel initialized with app-wide generation defaults.
        /// </summary>
        private static SchedulerOptionsModel CreateSchedulerOptionsFromDefaults()
        {
            var model = new SchedulerOptionsModel();

            // Apply app-wide defaults if available
            var settings = App.GetService<AmuseSettings>();
            var defaults = settings?.GenerationDefaults;
            if (defaults != null)
            {
                if (defaults.DefaultWidth > 0)
                    model.Width = defaults.DefaultWidth;
                if (defaults.DefaultHeight > 0)
                    model.Height = defaults.DefaultHeight;
                if (defaults.DefaultSteps > 0)
                    model.InferenceSteps = defaults.DefaultSteps;
                if (defaults.DefaultGuidanceScale > 0)
                    model.GuidanceScale = defaults.DefaultGuidanceScale;
            }

            // Reset HasChanged since we just initialized
            model.HasChanged = false;
            return model;
        }

        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand GenerateCommand { get; }
        public AsyncRelayCommand SaveHistoryCommand { get; }
        public AsyncRelayCommand ClearHistoryCommand { get; }
        public AsyncRelayCommand<ImageResult> SaveImageCommand { get; }
        public AsyncRelayCommand<ImageResult> CopyImageCommand { get; }
        public AsyncRelayCommand<ImageResult> RemoveImageCommand { get; }
        public AsyncRelayCommand<ImageResult> PreviewImageCommand { get; }
        public AsyncRelayCommand<int> UpdateSeedCommand { get; }
        public List<DiffuserType> SupportedDiffusers { get; init; }
        public ObservableCollection<ImageResult> ImageResults { get; }

        public PromptOptionsModel PromptOptions
        {
            get { return _promptOptionsModel; }
            set { _promptOptionsModel = value; NotifyPropertyChanged(); }
        }

        public SchedulerOptionsModel SchedulerOptions
        {
            get { return _schedulerOptions; }
            set { _schedulerOptions = value; NotifyPropertyChanged(); }
        }

        public BatchOptionsModel BatchOptions
        {
            get { return _batchOptions; }
            set { _batchOptions = value; NotifyPropertyChanged(); }
        }

        public ImageResult ResultImage
        {
            get { return _resultImage; }
            set { _resultImage = value; NotifyPropertyChanged(); }
        }

        public int SelectedTabIndex
        {
            get { return _selectedTabIndex; }
            set { _selectedTabIndex = value; NotifyPropertyChanged(); }
        }

        public bool IsHistoryView
        {
            get { return _isHistoryView; }
            set { _isHistoryView = value; NotifyPropertyChanged(); }
        }

        public bool RealtimeHasChanged
        {
            get { return _realtimeHasChanged; }
            set { _realtimeHasChanged = value; NotifyPropertyChanged(); }
        }

        public ImageInput InputImage
        {
            get { return _inputImage; }
            set
            {
                _inputImage = value;
                _inputImageCached = null;
                _inputControlImageCached = null;
                NotifyPropertyChanged();
            }
        }

        public BitmapSource PreviewResult
        {
            get { return _previewResult; }
            set { _previewResult = value; NotifyPropertyChanged(); }
        }

        public ImageInput SourceImage
        {
            get { return _sourceImage; }
            set { _sourceImage = value; NotifyPropertyChanged(); }
        }


        /// <summary>
        /// Called on Navigate
        /// </summary>
        /// <param name="navigationResult">The image result.</param>
        /// <returns></returns>
        public virtual async Task NavigateAsync(IImageResult navigationResult)
        {
            if (IsGenerating)
                await Cancel();

            Reset();
            ResultImage = null;
            if (navigationResult.Image != null)
            {
                SourceImage = new ImageInput
                {
                    Image = navigationResult.Image,
                    FileName = "Generated Image"
                };
            }
            if (navigationResult is ImageResult imageResult)
            {
                if (imageResult.Pipeline.BaseModel.ModelSet.Diffusers.Intersect(SupportedDiffusers).Any())
                {
                    if (SelectedBaseModel is null)
                        SelectedBaseModel = imageResult.Pipeline.BaseModel;

                    if (SelectedUpscaleModel is null)
                    {
                        SelectedUpscaleModel = imageResult.Pipeline.UpscaleModel;
                        IsUpscalerEnabled = SelectedUpscaleModel is not null;
                    }

                    if (IsControlNetSupported)
                    {
                        if (SelectedControlNetModel is null)
                        {
                            SelectedControlNetModel = imageResult.Pipeline.ControlNetModel;
                            IsControlNetEnabled = SelectedControlNetModel is not null;
                        }
                    }

                    if (IsFeatureExtractorEnabled)
                    {
                        if (SelectedFeatureExtractorModel is null)
                        {
                            SelectedFeatureExtractorModel = imageResult.Pipeline.FeatureExtractorModel;
                            IsFeatureExtractorEnabled = SelectedFeatureExtractorModel is not null;
                        }
                    }
                }
                PromptOptions = PromptOptionsModel.FromGenerateOptions(imageResult.PromptOptions);
                SchedulerOptions = SchedulerOptionsModel.FromSchedulerOptions(imageResult.SchedulerOptions);
            }
            SelectedTabIndex = 0;
            IsHistoryView = false;
        }


        public Task NavigateAsync(IVideoResult videoResult)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Generates this image result.
        /// </summary>
        protected virtual async Task Generate()
        {
            IDisposable generationLock = null;
            try
            {
                ResultImage = null;
                IsGenerating = true;
                ImageResults.Add(new ImageResult());

                // Acquire generation lock to prevent concurrent GPU access with API jobs
                if (_jobQueueService != null)
                {
                    UpdateProgress("Waiting for generation lock...");
                    generationLock = await _jobQueueService.AcquireGenerationLockAsync();
                }

                using (CancelationTokenSource = new CancellationTokenSource())
                {
                    if (await ModeratorService.ContainsExplicitContentAsync(PromptOptions.Prompt))
                        throw new OperationCanceledException();

                    if (BatchOptions.IsRealtimeEnabled)
                    {
                        await foreach (var resultImage in GenerateImageRealtimeAsync(CancelationTokenSource.Token))
                        {
                            if (resultImage is null)
                                continue;

                            ResultImage = resultImage;
                            if (BatchOptions.DisableHistory)
                                continue;

                            if (ImageResults.Count > Settings.HistoryMaxItems)
                                ImageResults.RemoveAt(0);
                            ImageResults.Remove(x => x.Image is null);
                            ImageResults.Add(resultImage);
                            ImageResults.Add(new ImageResult());

                            Statistics.Reset();
                        }
                    }
                    else if (BatchOptions.IsAutomationEnabled)
                    {
                        await foreach (var resultImage in GenerateImageBatchAsync(CancelationTokenSource.Token))
                        {
                            if (resultImage is null)
                                continue;

                            ResultImage = resultImage;
                            if (BatchOptions.DisableHistory)
                                continue;

                            if (ImageResults.Count > Settings.HistoryMaxItems)
                                ImageResults.RemoveAt(0);
                            ImageResults.Remove(x => x.Image is null);
                            ImageResults.Add(resultImage);
                            ImageResults.Add(new ImageResult());

                            Statistics.Reset();
                        }
                    }
                    else
                    {
                        var resultImage = await GenerateImageResultAsync(CancelationTokenSource.Token);
                        ResultImage = resultImage;

                        if (ImageResults.Count > Settings.HistoryMaxItems)
                            ImageResults.RemoveAt(0);
                        ImageResults.Remove(x => x.Image is null);
                        ImageResults.Add(resultImage);
                    }

                    StopStatistics();
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Generate was canceled");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during Generate\n{ex}", ex);
                await App.UIInvokeAsync(() => DialogService.ShowErrorMessageAsync("Generate Error", ex.Message));
                await UnloadPipelineAsync();
            }
            finally
            {
                generationLock?.Dispose();
            }
            Reset();
        }


        /// <summary>
        /// Determines whether this instance can execute Generate.
        /// </summary>
        protected virtual bool CanExecuteGenerate()
        {
            return !IsGenerating
               && CurrentPipeline?.IsLoaded == true
               && !CanLoadPipeline();
        }


        /// <summary>
        /// Cancels this generation.
        /// </summary>
        /// <returns></returns>
        protected virtual Task Cancel()
        {
            CancelationTokenSource?.Cancel();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether this instance can execute Cancel.
        /// </summary>
        private bool CanExecuteCancel()
        {
            return IsGenerating;
        }


        /// <summary>
        /// Clears the history.
        /// </summary>
        protected virtual Task ClearHistory()
        {
            ImageResults.Clear();
            ResultImage = null;
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether this instance can execute ClearHistory.
        /// </summary>
        protected virtual bool CanExecuteClearHistory()
        {
            return ImageResults.Count > 0;
        }


        /// <summary>
        /// Saves the history.
        /// </summary>
        private async Task SaveHistory()
        {
            var createVideoDialog = DialogService.GetDialog<CreateVideoDialog>();
            await createVideoDialog.ShowDialogAsync(ImageResults.Select(x => x.OnnxImage).Where(x => x is not null));
        }


        /// <summary>
        /// Determines whether this instance can execute SaveHistory.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if this instance [can execute save history]; otherwise, <c>false</c>.
        /// </returns>
        private bool CanExecuteSaveHistory()
        {
            return ImageResults.Count > 0;
        }


        /// <summary>
        /// Resets this instance.
        /// </summary>
        protected virtual void Reset()
        {
            StopStatistics();
            IsGenerating = false;
            ImageResults.Remove(x => x.Image is null);
            ClearProgress();
            PreviewResult = null;
        }


        /// <summary>
        /// Saves the image.
        /// </summary>
        /// <param name="result">The result.</param>
        private async Task SaveImage(ImageResult result)
        {
            if (result == null)
                return;

            await FileService.SaveAsImageFile(result);
        }


        /// <summary>
        /// Copies the image.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns></returns>
        private Task CopyImage(ImageResult result)
        {
            if (result == null)
                return Task.CompletedTask;

            Clipboard.SetImage(result.Image);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Updates the seed.
        /// </summary>
        /// <param name="seedValue">The seed value.</param>
        /// <returns></returns>
        private Task UpdateSeed(int seedValue)
        {
            if (seedValue <= 0)
                seedValue = Random.Shared.Next();

            SchedulerOptions.Seed = seedValue;
            return Task.CompletedTask;
        }


        /// <summary>
        /// Previews the image.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private async Task PreviewImage(ImageResult result)
        {
            if (result == null)
                return;

            var previewDialog = DialogService.GetDialog<PreviewImageDialog>();
            await previewDialog.ShowDialogAsync("Image Preview", result);
        }


        /// <summary>
        /// Removes the image.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns></returns>
        private Task RemoveImage(ImageResult result)
        {
            if (result == null)
                return Task.CompletedTask;

            ImageResults.Remove(result);
            if (result == ResultImage)
            {
                PreviewResult = null;
                ResultImage = null;
                ClearStatistics();
            }
            return Task.CompletedTask;
        }


        /// <summary>
        /// Handles API job completion events for history integration.
        /// Adds API-generated images to this view's history if the diffuser type matches.
        /// </summary>
        private async void OnApiJobCompleted(object sender, JobCompletedEventArgs e)
        {
            // Only process API jobs
            if (e.Source != "API")
                return;

            // Check if this view handles this diffuser type
            if (SupportedDiffusers == null || !SupportedDiffusers.Contains(e.Result.DiffuserType))
                return;

            // Ensure we have the OnnxImage
            if (e.Result.OnnxImage == null)
                return;

            try
            {
                // Create ImageResult on the UI thread
                await App.UIInvokeAsync(async () =>
                {
                    // Find the model by name to create a Pipeline reference
                    var modelViewModel = Settings?.StableDiffusionModelSets?
                        .FirstOrDefault(m => m.Name.Equals(e.Result.ModelName, StringComparison.OrdinalIgnoreCase));

                    if (modelViewModel == null)
                    {
                        Logger?.LogWarning("[API History] Could not find model '{ModelName}' for history entry", e.Result.ModelName);
                        return;
                    }

                    // Create a minimal pipeline for history display
                    var pipeline = new StableDiffusionPipelineModel(
                        Settings,
                        modelViewModel,
                        null, // ControlNet
                        null, // Upscale
                        null, // FeatureExtractor
                        null  // ContentFilter
                    );

                    var imageResult = new ImageResult
                    {
                        Image = await e.Result.OnnxImage.ToBitmapAsync(),
                        OnnxImage = e.Result.OnnxImage,
                        Pipeline = pipeline,
                        PromptOptions = e.Result.GenerateOptions,
                        PipelineType = e.Result.PipelineType,
                        DiffuserType = e.Result.DiffuserType,
                        SchedulerOptions = e.Result.GenerateOptions?.SchedulerOptions ?? new SchedulerOptions
                        {
                            Seed = e.Result.Seed,
                            Width = e.Result.Width,
                            Height = e.Result.Height,
                            InferenceSteps = e.Result.InferenceSteps,
                            GuidanceScale = e.Result.GuidanceScale
                        },
                        Elapsed = e.Result.ElapsedSeconds
                    };

                    // Add to history with size limit enforcement
                    if (ImageResults.Count > Settings.HistoryMaxItems)
                        ImageResults.RemoveAt(0);

                    ImageResults.Add(imageResult);

                    // Auto-save if enabled
                    await FileService.AutoSaveImageFile(imageResult, $"API_{e.Result.DiffuserType}");

                    Logger?.LogInformation("[API History] Added API job {JobId} to {ViewType} history",
                        e.JobId, GetType().Name);
                });
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "[API History] Failed to add API job {JobId} to history", e.JobId);
            }
        }


        /// <summary>
        /// Gets the generate options.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected virtual async Task<GenerateOptions> GetGenerateOptionsAsync(CancellationToken cancellationToken)
        {
            var generateOptions = PromptOptionsModel.ToGenerateOptions(PromptOptions, SchedulerOptions, MemoryInfo);
            if (IsControlNetEnabled)
            {
                generateOptions.ControlNet = await LoadControlNetAsync();
                var controlNetDiffuserType = SchedulerOptions.Strength >= 1 && CurrentPipeline.ModelType != ModelType.Instruct
                        ? DiffuserType.ControlNet
                        : DiffuserType.ControlNetImage;

                if (_inputControlImageCached == null)
                    _inputControlImageCached = await GetInputImage(InputImage, true, cancellationToken);

                if (controlNetDiffuserType == DiffuserType.ControlNetImage)
                {
                    if (_inputImageCached == null)
                        _inputImageCached = await GetInputImage(InputImage, false, cancellationToken);

                    generateOptions.InputImage = _inputImageCached;
                }

                generateOptions.InputContolImage = _inputControlImageCached;
                generateOptions.Diffuser = controlNetDiffuserType;
                return generateOptions;
            }

            if (_inputImageCached == null)
                _inputImageCached = await GetInputImage(InputImage, true, cancellationToken);

            generateOptions.Diffuser = DiffuserType.ImageToImage;
            generateOptions.InputImage = _inputImageCached;
            return generateOptions;
        }


        private async Task<OnnxImage> GetInputImage(ImageInput inputImage, bool extractFeatures = false, CancellationToken cancellationToken = default)
        {
            if (inputImage == null)
                return default;

            var imageBytes = inputImage.Image.GetImageBytes();
            if (imageBytes.IsNullOrEmpty())
                return default;

            if (extractFeatures)
                return await ExecuteFeatureExtractorAsync(new OnnxImage(imageBytes), cancellationToken);

            return new OnnxImage(imageBytes);
        }



        /// <summary>
        /// Resets the control image cache.
        /// </summary>
        protected virtual void ResetControlImageCache()
        {
            _inputImageCached = null;
            _inputControlImageCached = null;
        }


        /// <summary>
        /// Gets the batch options.
        /// </summary>
        /// <returns></returns>
        protected virtual GenerateBatchOptions GetBatchOptions(GenerateOptions generateOptions)
        {
            return new GenerateBatchOptions(generateOptions)
            {
                BatchType = BatchOptions.BatchType,
                ValueTo = BatchOptions.ValueTo,
                Increment = BatchOptions.Increment,
                ValueFrom = BatchOptions.ValueFrom
            };
        }


        /// <summary>
        /// Generates the result asynchronous.
        /// </summary>
        /// <param name="onnxImage">The onnx image.</param>
        /// <param name="promptOptions">The prompt options.</param>
        /// <param name="schedulerOptions">The scheduler options.</param>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns></returns>
        protected virtual async Task<ImageResult> GenerateResultAsync(OnnxImage onnxImage, GenerateOptions options, long timestamp)
        {
            // Ensure result is create in UI thread
            return await App.UIInvokeAsync(async () =>
            {
                var imageResult = new ImageResult
                {
                    Image = await onnxImage.ToBitmapAsync(),
                    OnnxImage = onnxImage,
                    Pipeline = CurrentPipeline,
                    PromptOptions = options,
                    PipelineType = SelectedBaseModel.ModelSet.PipelineType,
                    DiffuserType = options.Diffuser,
                    SchedulerOptions = options.SchedulerOptions,
                    Elapsed = Stopwatch.GetElapsedTime(timestamp).TotalSeconds
                };

                await FileService.AutoSaveImageFile(imageResult, options.Diffuser.ToString());
                return imageResult;
            }, System.Windows.Threading.DispatcherPriority.Send);
        }


        /// <summary>
        /// Updates the progress.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <returns></returns>
        protected override async Task UpdateProgress(DiffusionProgress progress)
        {
            UpdateStatistics(progress);
            if (BatchOptions.IsRealtimeEnabled)
            {
                if (BatchOptions.StepsValue != progress.StepMax)
                    BatchOptions.StepsValue = progress.StepMax;
                if (BatchOptions.StepValue != progress.StepValue)
                    BatchOptions.StepValue = progress.StepValue;

                UpdateProgress(progress.StepValue, progress.StepMax, progress.Message);
            }
            else if (BatchOptions.IsAutomationEnabled)
            {
                if (BatchOptions.BatchsValue != progress.BatchMax)
                    BatchOptions.BatchsValue = progress.BatchMax;
                if (BatchOptions.BatchValue != progress.BatchValue)
                    BatchOptions.BatchValue = progress.BatchValue;
                if (BatchOptions.StepsValue != progress.StepMax)
                    BatchOptions.StepsValue = progress.StepMax;
                if (BatchOptions.StepValue != progress.StepValue)
                    BatchOptions.StepValue = progress.StepValue;

                UpdateProgress(progress.StepValue, progress.StepMax, progress.Message);
                if (progress.StepTensor is not null)
                    PreviewResult = await PreviewService.GeneratePreview(progress.StepTensor);
            }
            else
            {
                UpdateProgress(progress.StepValue, progress.StepMax, progress.Message);
                if (progress.StepTensor is not null)
                    PreviewResult = await PreviewService.GeneratePreview(progress.StepTensor);
            }

            await base.UpdateProgress(progress);
        }


        /// <summary>
        /// Updates the progress Image.
        /// </summary>
        /// <returns>Task.</returns>
        protected override Task UpdateProgressImage(DiffusionProgress progress)
        {
            if (ImageResults.Count > 0)
                ImageResults[^1].PreviewImage = PreviewResult;

            return base.UpdateProgressImage(progress);
        }


        /// <summary>
        /// Executes the stable diffusion process.
        /// </summary>
        /// <param name="promptOptions">The prompt options.</param>
        /// <param name="schedulerOptions">The scheduler options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected async Task<OnnxImage> ExecuteStableDiffusionAsync(GenerateOptions options, CancellationToken cancellationToken)
        {
            StartStatistics();
            var stableDiffusionPipeline = await LoadBaseModelAsync();
            return await Task.Run(() => stableDiffusionPipeline.GenerateAsync(options, ProgressCallback, cancellationToken));
        }


        /// <summary>
        /// Executes the stable diffusion batch process.
        /// </summary>
        /// <param name="batchOptions">The batch options.</param>
        /// <param name="promptOptions">The prompt options.</param>
        /// <param name="schedulerOptions">The scheduler options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected async IAsyncEnumerable<BatchImageResult> ExecuteStableDiffusionBatchAsync(GenerateBatchOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            StartStatistics();
            var stableDiffusionPipeline = await LoadBaseModelAsync();

            await foreach (var result in stableDiffusionPipeline.GenerateBatchAsync(options, ProgressCallback, cancellationToken).ConfigureAwait(false))
                yield return result;
        }


        /// <summary>
        /// Executes the upscaler.
        /// </summary>
        /// <param name="inputImage">The input image.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected async Task<OnnxImage> ExecuteUpscalerAsync(OnnxImage inputImage, CancellationToken cancellationToken)
        {
            if (CurrentPipeline.UpscaleModel is not null)
            {
                var upscalePipeline = await LoadUpscalerAsync();
                UpdateProgress("Upscaling Image...", true);
                var tileMode = CurrentPipeline.UpscaleModel.ModelSet.TileMode;
                var tileSize = CurrentPipeline.UpscaleModel.ModelSet.TileSize;
                var tileOverlap = CurrentPipeline.UpscaleModel.ModelSet.TileOverlap;
                var isLowMemory = MemoryInfo.IsLowMemoryPipelineEnabled;
                var options = new UpscaleOptions(tileMode, tileSize, tileOverlap, isLowMemory);
                var result = await Task.Run(() => upscalePipeline.RunAsync(inputImage, options, cancellationToken));
                return result;
            }
            return inputImage;
        }


        /// <summary>
        /// Executes the feature extractor.
        /// </summary>
        /// <param name="inputImage">The input image.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected async Task<OnnxImage> ExecuteFeatureExtractorAsync(OnnxImage inputImage, CancellationToken cancellationToken)
        {
            if (CurrentPipeline.FeatureExtractorModel is not null)
            {
                var featureExtractorPipeline = await LoadFeatureExtractorAsync();
                UpdateProgress("Extracting Image Feature...", true);
                var isLowMemoryEnabled = MemoryInfo.IsLowMemoryPipelineEnabled;
                var options = new FeatureExtractorOptions(isLowMemoryEnabled: isLowMemoryEnabled);
                var result = await Task.Run(() => featureExtractorPipeline.RunAsync(inputImage, options, cancellationToken));
                return result;
            }
            return inputImage;
        }


        /// <summary>
        /// Executes the ContentFilter.
        /// </summary>
        /// <param name="inputImage">The input image.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected virtual async Task<OnnxImage> ExecuteContentFilterAsync(OnnxImage inputImage, CancellationToken cancellationToken)
        {
            if (CurrentPipeline.ContentFilterModel is not null && ModeratorService.IsContentFilterEnabled)
            {
                var stopwatch = Stopwatch.GetTimestamp();
                var contentFilterPipeline = await LoadContentFilterAsync();
                // UpdateProgress("Running Content Filter...", true);
                var isLowMemoryEnabled = MemoryInfo.IsLowMemoryPipelineEnabled;
                var result = await Task.Run(() => contentFilterPipeline.RunAsync(inputImage, 0.030f, isLowMemoryEnabled, cancellationToken));
                return result;
            }
            return inputImage;
        }


        protected async Task<ImageResult> GenerateImageResultAsync(CancellationToken cancellationToken)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var generateOptions = await GetGenerateOptionsAsync(cancellationToken);
            var result = await ExecuteStableDiffusionAsync(generateOptions, cancellationToken);
            result = await ExecuteContentFilterAsync(result, cancellationToken);
            result = await ExecuteUpscalerAsync(result, cancellationToken);
            return await GenerateResultAsync(result, generateOptions, timestamp);
        }


        protected async IAsyncEnumerable<ImageResult> GenerateImageBatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var generateOptions = await GetGenerateOptionsAsync(cancellationToken);
            var batchGenerateOptions = GetBatchOptions(generateOptions);

            await foreach (var batchResult in ExecuteStableDiffusionBatchAsync(batchGenerateOptions, cancellationToken))
            {
                var result = await ExecuteContentFilterAsync(batchResult.Result, cancellationToken);
                result = await ExecuteUpscalerAsync(result, cancellationToken);

                var batchResultOptions = generateOptions with { SchedulerOptions = batchResult.SchedulerOptions };
                yield return await GenerateResultAsync(result, batchResultOptions, timestamp);
            }
        }


        protected async IAsyncEnumerable<ImageResult> GenerateImageRealtimeAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            SchedulerOptions.Seed = SchedulerOptions.Seed == 0 ? Random.Shared.Next() : SchedulerOptions.Seed;
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(Settings.RealtimeRefreshRate);
                if (RealtimeHasChanged || SchedulerOptions.HasChanged || PromptOptions.HasChanged || SchedulerOptions.Seed == 0)
                {
                    RealtimeHasChanged = false;
                    PromptOptions.HasChanged = false;
                    SchedulerOptions.HasChanged = false;
                    var generateOptions = await GetGenerateOptionsAsync(cancellationToken);

                    generateOptions.Prompt = string.IsNullOrEmpty(generateOptions.Prompt) ? " " : generateOptions.Prompt;
                    var timestamp = Stopwatch.GetTimestamp();

                    var result = await ExecuteStableDiffusionAsync(generateOptions, cancellationToken);
                    result = await ExecuteContentFilterAsync(result, cancellationToken);
                    result = await ExecuteUpscalerAsync(result, cancellationToken);
                    yield return await GenerateResultAsync(result, generateOptions, timestamp);
                }
            }
        }
    }
}
