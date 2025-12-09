using Amuse.UI.Commands;
using Amuse.UI.Dialogs;
using Amuse.UI.Models;
using Amuse.UI.Models.StableDiffusion;
using Amuse.UI.Services;
using Microsoft.Extensions.Logging;
using OnnxStack.Core;
using OnnxStack.Core.Image;
using OnnxStack.Core.Video;
using OnnxStack.FeatureExtractor.Common;
using OnnxStack.StableDiffusion.Common;
using OnnxStack.StableDiffusion.Config;
using OnnxStack.StableDiffusion.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    public class EZModeViewBase : StableDiffusionViewBase
    {
        private readonly IDeviceService _deviceService;
        private PromptOptionsModel _promptOptionsModel;
        private SchedulerOptionsModel _schedulerOptions;
        private BatchOptionsModel _batchOptions;
        private int _selectedTabIndex;
        private bool _isHistoryView;
        private bool _realtimeHasChanged;
        private BitmapSource _previewResult;

        private ImageResult _resultImage;
        private ImageInput _inputImage;
        private OnnxImage _inputImageCached;
        private OnnxImage _inputControlImageCached;

        private VideoResultModel _resultVideo;
        private VideoInputModel _inputVideo;
        private OnnxVideo _inputVideoCached;
        private OnnxVideo _inputControlVideoCached;

        private bool _isVideoGenerationMode;
        private bool _videoSync;
        private IProgress<FeatureExtractorProgress> _extractorProgressCallback;
        private ImageInput _sourceImage;
        private VideoInputModel _sourceVideo;

        public EZModeViewBase()
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                _deviceService = App.GetService<IDeviceService>();
            }

            _extractorProgressCallback = CreateExtractorProgressCallback();
            PromptOptions = new PromptOptionsModel();
            SchedulerOptions = new SchedulerOptionsModel();
            BatchOptions = new BatchOptionsModel();
            UpdateSeedCommand = new AsyncRelayCommand<int>(UpdateSeed);
            CancelCommand = new AsyncRelayCommand(Cancel, CanExecuteCancel);
            ClearHistoryCommand = new AsyncRelayCommand(ClearHistory, CanExecuteClearHistory);
            VideoGenerationModeCommand = new RelayCommand<bool>(enable => IsVideoGenerationMode = enable);

            GenerateImageCommand = new AsyncRelayCommand(GenerateImage, CanExecuteGenerate);
            SaveImageCommand = new AsyncRelayCommand<ImageResult>(SaveImage);
            CopyImageCommand = new AsyncRelayCommand<ImageResult>(CopyImage);
            RemoveImageCommand = new AsyncRelayCommand<ImageResult>(RemoveImage);
            PreviewImageCommand = new AsyncRelayCommand<ImageResult>(PreviewImage);
            ImageResults = new ObservableCollection<ImageResult>();

            GenerateVideoCommand = new AsyncRelayCommand(GenerateVideo, CanExecuteGenerate);
            SaveVideoCommand = new AsyncRelayCommand<VideoResultModel>(SaveVideo);
            CopyVideoCommand = new AsyncRelayCommand<VideoResultModel>(CopyVideo);
            RemoveVideoCommand = new AsyncRelayCommand<VideoResultModel>(RemoveVideo);
            PreviewVideoCommand = new AsyncRelayCommand<VideoResultModel>(PreviewVideo);
            VideoResults = new ObservableCollection<VideoResultModel>();
        }

        public IDeviceService DeviceService => _deviceService;
        public List<DiffuserType> SupportedDiffusers { get; init; }

        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand ClearHistoryCommand { get; }
        public AsyncRelayCommand<int> UpdateSeedCommand { get; }

        public RelayCommand<bool> VideoGenerationModeCommand { get; }

        public AsyncRelayCommand GenerateImageCommand { get; }
        public AsyncRelayCommand<ImageResult> SaveImageCommand { get; }
        public AsyncRelayCommand<ImageResult> CopyImageCommand { get; }
        public AsyncRelayCommand<ImageResult> RemoveImageCommand { get; }
        public AsyncRelayCommand<ImageResult> PreviewImageCommand { get; }
        public ObservableCollection<ImageResult> ImageResults { get; }

        public AsyncRelayCommand GenerateVideoCommand { get; }
        public AsyncRelayCommand<VideoResultModel> SaveVideoCommand { get; }
        public AsyncRelayCommand<VideoResultModel> CopyVideoCommand { get; set; }
        public AsyncRelayCommand<VideoResultModel> RemoveVideoCommand { get; set; }
        public AsyncRelayCommand<VideoResultModel> PreviewVideoCommand { get; set; }
        public ObservableCollection<VideoResultModel> VideoResults { get; }


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

        public BitmapSource PreviewResult
        {
            get { return _previewResult; }
            set { _previewResult = value; NotifyPropertyChanged(); }
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

        public ImageResult ResultImage
        {
            get { return _resultImage; }
            set { _resultImage = value; NotifyPropertyChanged(); }
        }

        public VideoInputModel InputVideo
        {
            get { return _inputVideo; }
            set
            {
                _inputVideo = value;
                _inputVideoCached = null;
                _inputControlVideoCached = null;
                PromptOptions.VideoInputFPS = _inputVideo?.Video?.FrameRate ?? 15;
                PromptOptions.VideoOutputFPS = PromptOptions.VideoInputFPS;
                NotifyPropertyChanged();
            }
        }

        public VideoResultModel ResultVideo
        {
            get { return _resultVideo; }
            set { _resultVideo = value; NotifyPropertyChanged(); }
        }

        public bool IsVideoGenerationMode
        {
            get { return _isVideoGenerationMode; }
            set { _isVideoGenerationMode = value; NotifyPropertyChanged(); OnGenerationModeChanged(); }
        }

        protected OnnxImage InputImageCached
        {
            get { return _inputImageCached; }
            set { _inputImageCached = value; }
        }

        protected OnnxImage InputControlImageCached
        {
            get { return _inputControlImageCached; }
            set { _inputControlImageCached = value; }
        }

        public bool VideoSync
        {
            get { return _videoSync; }
            set { _videoSync = value; NotifyPropertyChanged(); }
        }

        public ImageInput SourceImage
        {
            get { return _sourceImage; }
            set { _sourceImage = value; NotifyPropertyChanged(); }
        }

        public VideoInputModel SourceVideo
        {
            get { return _sourceVideo; }
            set { _sourceVideo = value; NotifyPropertyChanged(); }
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
        protected virtual async Task ClearHistory()
        {
            ImageResults.Clear();
            ResultImage = null;

            await FileService.DeleteTempVideoFile(VideoResults);
            VideoResults.Clear();
            ResultVideo = null;
        }


        /// <summary>
        /// Determines whether this instance can execute ClearHistory.
        /// </summary>
        protected virtual bool CanExecuteClearHistory()
        {
            return ImageResults.Count > 0 || VideoResults.Count > 0 || ResultImage != null || ResultVideo != null;
        }


        /// <summary>
        /// Resets this instance.
        /// </summary>
        protected virtual void Reset()
        {
            IsGenerating = false;
            ClearProgress();
            PreviewResult = null;
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
        /// Called when GenerationMode changed.
        /// </summary>
        protected virtual void OnGenerationModeChanged()
        {
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
                if (progress.StepMax > 0)
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
                }
                else
                {
                    UpdateProgress(progress.Message);
                }

                if (progress.StepTensor is not null)
                    PreviewResult = await PreviewService.GeneratePreview(progress.StepTensor);
            }
            else
            {
                if (progress.StepMax > 0)
                {
                    UpdateProgress(progress.StepValue, progress.StepMax, progress.Message);
                }
                else
                {
                    UpdateProgress(progress.Message);
                }

                if (progress.StepTensor is not null)
                    PreviewResult = await PreviewService.GeneratePreview(progress.StepTensor);
            }

            await base.UpdateProgress(progress);
        }


        #region Image Generation


        protected virtual async Task GenerateImage()
        {
            try
            {
                ResultImage = null;
                IsGenerating = true;
                ImageResults.Add(new ImageResult());
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

                            ImageResults.Remove(x => x.Image is null);
                            ImageResults.Add(resultImage);
                            ImageResults.Add(new ImageResult());
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

                            ImageResults.Remove(x => x.Image is null);
                            ImageResults.Add(resultImage);
                            ImageResults.Add(new ImageResult());
                        }
                    }
                    else
                    {
                        var resultImage = await GenerateImageResultAsync(CancelationTokenSource.Token);
                        ResultImage = resultImage;
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
            Reset();
        }


        /// <summary>
        /// Saves the image.
        /// </summary>
        /// <param name="result">The result.</param>
        private async Task SaveImage(ImageResult result)
        {
            if (result == null || result.Image == null)
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
        /// Gets the generate options.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected virtual async Task<GenerateOptions> GetGenerateImageOptionsAsync(CancellationToken cancellationToken)
        {
            var generateOptions = PromptOptionsModel.ToGenerateOptions(PromptOptions, SchedulerOptions, MemoryInfo);
            if (SelectedControlNetModel is not null)
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


        protected async Task<OnnxImage> GetInputImage(ImageInput inputImage, bool extractFeatures = false, CancellationToken cancellationToken = default)
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
        /// Generates the result asynchronous.
        /// </summary>
        /// <param name="onnxImage">The onnx image.</param>
        /// <param name="promptOptions">The prompt options.</param>
        /// <param name="schedulerOptions">The scheduler options.</param>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns></returns>
        protected virtual async Task<ImageResult> GenerateImageResultAsync(OnnxImage onnxImage, GenerateOptions options, long timestamp)
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
        protected async Task<OnnxImage> ExecuteStableDiffusionImageAsync(GenerateOptions options, CancellationToken cancellationToken)
        {
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
        protected async IAsyncEnumerable<BatchImageResult> ExecuteStableDiffusionImageBatchAsync(GenerateBatchOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var stableDiffusionPipeline = await LoadBaseModelAsync();
            await foreach (var result in stableDiffusionPipeline.GenerateBatchAsync(options, ProgressCallback, cancellationToken).ConfigureAwait(false))
                yield return result;
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
                var result = await Task.Run(() => featureExtractorPipeline.RunAsync(inputImage, new FeatureExtractorOptions(), cancellationToken));
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
                var contentFilterPipeline = await LoadContentFilterAsync();
                // UpdateProgress("Running Content Filter...", true);
                var result = await Task.Run(() => contentFilterPipeline.RunAsync(inputImage, 0.030f, false, cancellationToken));
                return result;
            }
            return inputImage;
        }


        protected async Task<ImageResult> GenerateImageResultAsync(CancellationToken cancellationToken)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var generateOptions = await GetGenerateImageOptionsAsync(cancellationToken);
            var result = await ExecuteStableDiffusionImageAsync(generateOptions, cancellationToken);
            result = await ExecuteContentFilterAsync(result, cancellationToken);
            return await GenerateImageResultAsync(result, generateOptions, timestamp);
        }


        protected async IAsyncEnumerable<ImageResult> GenerateImageBatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var generateOptions = await GetGenerateImageOptionsAsync(cancellationToken);
            var batchGenerateOptions = GetBatchOptions(generateOptions);

            await foreach (var batchResult in ExecuteStableDiffusionImageBatchAsync(batchGenerateOptions, cancellationToken))
            {
                var result = await ExecuteContentFilterAsync(batchResult.Result, cancellationToken);
                var batchResultOptions = generateOptions with { SchedulerOptions = batchResult.SchedulerOptions };
                yield return await GenerateImageResultAsync(result, batchResultOptions, timestamp);
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
                    var generateOptions = await GetGenerateImageOptionsAsync(cancellationToken);

                    generateOptions.Prompt = string.IsNullOrEmpty(generateOptions.Prompt) ? " " : generateOptions.Prompt;
                    var timestamp = Stopwatch.GetTimestamp();

                    var result = await ExecuteStableDiffusionImageAsync(generateOptions, cancellationToken);
                    result = await ExecuteContentFilterAsync(result, cancellationToken);
                    yield return await GenerateImageResultAsync(result, generateOptions, timestamp);
                }
            }
        }

        #endregion


        #region Video Generation


        /// <summary>
        /// Generates this image result.
        /// </summary>
        protected virtual async Task GenerateVideo()
        {
            try
            {
                IsGenerating = true;
                ResultVideo = null;
                VideoResults.Add(new VideoResultModel());
                using (CancelationTokenSource = new CancellationTokenSource())
                {
                    if (await ModeratorService.ContainsExplicitContentAsync(PromptOptions.Prompt))
                        throw new OperationCanceledException();

                    if (BatchOptions.IsAutomationEnabled)
                    {
                        await foreach (var resultVideo in GenerateVideoBatchAsync(CancelationTokenSource.Token))
                        {
                            if (resultVideo is null)
                                continue;

                            ResultVideo = resultVideo;
                            if (BatchOptions.DisableHistory)
                                continue;

                            VideoResults.Remove(x => x.Video is null);
                            VideoResults.Add(resultVideo);
                            VideoResults.Add(new VideoResultModel());
                        }
                    }
                    else
                    {
                        var resultImage = await GenerateVideoResultAsync(CancelationTokenSource.Token);
                        ResultVideo = resultImage;
                        VideoResults.Remove(x => x.Video is null);
                        VideoResults.Add(resultImage);
                    }

                    StopStatistics();
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation($"Generate was canceled.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during Generate\n{ex}", ex);
                await App.UIInvokeAsync(() => DialogService.ShowErrorMessageAsync("Generate Error", ex.Message));
                await UnloadPipelineAsync();
            }
            Reset();
        }


        /// <summary>
        /// Saves the video.
        /// </summary>
        /// <param name="result">The result.</param>
        private async Task SaveVideo(VideoResultModel result)
        {
            if (result == null || result.Video == null)
                return;

            await FileService.SaveAsVideoFile(result);
        }


        /// <summary>
        /// Copies the video.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns></returns>
        private Task CopyVideo(VideoResultModel result)
        {
            if (result == null)
                return Task.CompletedTask;

            Clipboard.SetFileDropList(new StringCollection
            {
                result.FileName
            });
            return Task.CompletedTask;
        }


        /// <summary>
        /// Removes the video.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns></returns>
        private async Task RemoveVideo(VideoResultModel result)
        {
            if (result == null)
                return;

            await FileService.DeleteTempVideoFile(result);
            VideoResults.Remove(result);
            if (result == ResultVideo)
            {
                ResultVideo = null;
                ClearStatistics();
            }
        }


        /// <summary>
        /// Gets the prompt options
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected virtual async Task<GenerateOptions> GetGenerateVideoOptionsAsync(CancellationToken cancellationToken)
        {
            var inputFPS = PromptOptions.VideoInputFPS;
            var generateOptions = PromptOptionsModel.ToGenerateVideoOptions(PromptOptions, SchedulerOptions, MemoryInfo);
            if (_inputVideoCached == null || _inputVideoCached.FrameRate != inputFPS || _inputVideoCached.Width != SchedulerOptions.Width || _inputVideoCached.Height != SchedulerOptions.Height)
            {
                UpdateProgress("Processing Input Video...");
                int? videoWidth = _inputVideo.Video.Height > _inputVideo.Video.Width ? SelectedBaseModel.ModelSet.SampleSize : null;
                int? videoHeight = _inputVideo.Video.Width > _inputVideo.Video.Height ? SelectedBaseModel.ModelSet.SampleSize : null;
                _inputControlVideoCached = null;
                _inputVideoCached = await OnnxVideo.FromFileAsync(_inputVideo.FileName, inputFPS, videoWidth, videoHeight, CancelationTokenSource.Token);
                _inputVideoCached.Resize(SchedulerOptions.Height, SchedulerOptions.Width);
            }

            if (SelectedControlNetModel is not null)
            {
                generateOptions.ControlNet = await LoadControlNetAsync();
                var controlNetDiffuserType = SchedulerOptions.Strength >= 1 && CurrentPipeline.ModelType != ModelType.Instruct
                        ? DiffuserType.ControlNet
                        : DiffuserType.ControlNetImage;

                if (_inputControlVideoCached == null)
                    _inputControlVideoCached = await ExecuteFeatureExtractorAsync(_inputVideoCached, cancellationToken);

                generateOptions.InputVideo = _inputVideoCached;
                generateOptions.InputContolVideo = _inputControlVideoCached;
                generateOptions.Diffuser = controlNetDiffuserType;
                return generateOptions;
            }

            if (_inputVideoCached != null)
                _inputVideoCached = await ExecuteFeatureExtractorAsync(_inputVideoCached, cancellationToken);

            generateOptions.Diffuser = DiffuserType.ImageToImage;
            generateOptions.InputVideo = _inputVideoCached;
            return generateOptions;
        }


        private async Task PreviewVideo(VideoResultModel result)
        {
            var previewDialog = DialogService.GetDialog<PreviewVideoDialog>();
            await previewDialog.ShowDialogAsync("Video Preview", result);
        }


        /// <summary>
        /// Resets the control image cache.
        /// </summary>
        protected virtual void ResetControlVideoCache()
        {
            _inputControlVideoCached = null;
        }


        /// <summary>
        /// Generates the result.
        /// </summary>
        /// <param name="imageBytes">The image bytes.</param>
        /// <param name="promptOptions">The prompt options.</param>
        /// <param name="schedulerOptions">The scheduler options.</param>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns></returns>
        protected async Task<VideoResultModel> GenerateVideoResultAsync(OnnxVideo video, GenerateOptions generateOptions, long timestamp)
        {
            video.NormalizeBrightness();
            return await App.UIInvokeAsync(async () =>
            {
                var tempvideoFileName = await FileService.SaveTempVideoFile(video, "VideoToVideo");
                var videoResult = new VideoResultModel
                {
                    Video = video,
                    ModelName = SelectedBaseModel.Name,
                    FileName = tempvideoFileName,
                    PromptOptions = generateOptions,
                    SchedulerOptions = generateOptions.SchedulerOptions,
                    DiffuserType = generateOptions.Diffuser,
                    PipelineType = SelectedBaseModel.ModelSet.PipelineType,
                    Elapsed = Stopwatch.GetElapsedTime(timestamp).TotalSeconds,
                    Thumbnail = await video.GetFrame(1).ToBitmapAsync()
                };

                await FileService.AutoSaveVideoFile(videoResult, "VideoToVideo");
                return videoResult;
            });
        }


        /// <summary>
        /// Executes the stable diffusion process.
        /// </summary>
        /// <param name="promptOptions">The prompt options.</param>
        /// <param name="schedulerOptions">The scheduler options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected async Task<OnnxVideo> ExecuteStableDiffusionVideoAsync(GenerateOptions options, CancellationToken cancellationToken)
        {
            var stableDiffusionPipeline = await LoadBaseModelAsync();
            return await stableDiffusionPipeline.GenerateVideoAsync(options, ProgressCallback, cancellationToken);
        }


        /// <summary>
        /// Executes the feature extractor.
        /// </summary>
        /// <param name="inputVideo">The input video.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected async Task<OnnxVideo> ExecuteFeatureExtractorAsync(OnnxVideo inputVideo, CancellationToken cancellationToken)
        {
            if (CurrentPipeline.FeatureExtractorModel is not null)
            {
                var featureExtractorPipeline = await LoadFeatureExtractorAsync();
                UpdateProgress("Extracting Video Feature...", true);
                var result = await featureExtractorPipeline.RunAsync(inputVideo, new FeatureExtractorOptions(), _extractorProgressCallback, cancellationToken: cancellationToken);
                ClearProgress();
                return result;
            }
            return inputVideo;
        }
     

        protected virtual async Task<OnnxVideo> ExecuteContentFilterAsync(OnnxVideo inputVideo, CancellationToken cancellationToken)
        {
            if (CurrentPipeline.ContentFilterModel is not null && ModeratorService.IsContentFilterEnabled)
            {
                var contentFilterPipeline = await LoadContentFilterAsync();
                //UpdateProgress("Content Filter...", true);
                var result = await Task.Run(() => contentFilterPipeline.RunAsync(inputVideo, 0.030f, cancellationToken: cancellationToken));
                return result;
            }
            return inputVideo;
        }


        protected virtual async Task<VideoResultModel> GenerateVideoResultAsync(CancellationToken cancellationToken)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var generateOptions = await GetGenerateVideoOptionsAsync(cancellationToken);
            var result = await ExecuteStableDiffusionVideoAsync(generateOptions, cancellationToken);
            result = await ExecuteContentFilterAsync(result, cancellationToken);
            return await GenerateVideoResultAsync(result, generateOptions, timestamp);
        }


        protected virtual async IAsyncEnumerable<VideoResultModel> GenerateVideoBatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var defaultGenerateOptions = await GetGenerateVideoOptionsAsync(cancellationToken);
            var batchOptions = GetBatchOptions(defaultGenerateOptions);
            var batchGenerateOptions = GenerateVideoBatchOptions(batchOptions, defaultGenerateOptions.SchedulerOptions);
            BatchOptions.BatchValue = 0;
            BatchOptions.BatchsValue = batchGenerateOptions.Count;
            foreach (var batchGenerateOption in batchGenerateOptions)
            {
                var generateOptions = defaultGenerateOptions with
                {
                    SchedulerOptions = batchGenerateOption
                };
                var result = await ExecuteStableDiffusionVideoAsync(generateOptions, cancellationToken);
                result = await ExecuteContentFilterAsync(result, cancellationToken);
                yield return await GenerateVideoResultAsync(result, generateOptions, timestamp);
                BatchOptions.BatchValue++;
            }
        }


        private List<SchedulerOptions> GenerateVideoBatchOptions(GenerateBatchOptions batchOptions, SchedulerOptions schedulerOptions)
        {
            var seed = schedulerOptions.Seed == 0 ? Random.Shared.Next() : schedulerOptions.Seed;
            if (batchOptions.BatchType == BatchOptionType.Seed)
            {

                if (batchOptions.ValueTo <= 1)
                    return [schedulerOptions with { Seed = seed }];

                var random = new Random(seed);
                return Enumerable.Range(0, Math.Max(1, (int)batchOptions.ValueTo - 1))
                    .Select(x => random.Next())
                    .Prepend(seed)
                    .Select(x => schedulerOptions with { Seed = x })
                    .ToList();
            }

            if (batchOptions.BatchType == BatchOptionType.Scheduler)
            {
                return CurrentPipeline.SupportedSchedulers
                  .Select(x => schedulerOptions with { SchedulerType = x })
                  .ToList();
            }

            var totalIncrements = (int)Math.Max(1, (batchOptions.ValueTo - batchOptions.ValueFrom) / batchOptions.Increment) + 1;
            if (batchOptions.BatchType == BatchOptionType.Step)
            {
                return Enumerable.Range(0, totalIncrements)
                   .Select(x => schedulerOptions with { Seed = seed, InferenceSteps = (int)(batchOptions.ValueFrom + (batchOptions.Increment * x)) })
                   .ToList();
            }

            if (batchOptions.BatchType == BatchOptionType.Guidance)
            {
                return Enumerable.Range(0, totalIncrements)
                  .Select(x => schedulerOptions with { Seed = seed, GuidanceScale = batchOptions.ValueFrom + (batchOptions.Increment * x) })
                  .ToList();
            }

            return new List<SchedulerOptions>();
        }


        /// <summary>
        /// Synchronizes the InputVideo and ResultVideo.
        /// </summary>
        /// <returns>Task.</returns>
        protected Task SyncVideoAsync()
        {
            if (InputVideo == null || ResultVideo == null)
                return Task.CompletedTask;

            if (InputVideo.Video.Duration != ResultVideo.Video.Duration)
                return Task.CompletedTask;

            VideoSync = !VideoSync;
            return Task.CompletedTask;
        }


        private IProgress<FeatureExtractorProgress> CreateExtractorProgressCallback()
        {
            return new Progress<FeatureExtractorProgress>((progress) =>
            {
                if (CancelationTokenSource.IsCancellationRequested)
                    return;

                var frame = Progress.Value + 1;
                var frames = InputVideo.Video.FrameCount;
                UpdateProgress(frame, frames, $"Extracting Feature: {frame:D2}/{frames}");
            });
        }

        #endregion
    }



}
