using Amuse.UI.Commands;
using Amuse.UI.Exceptions;
using Amuse.UI.Models;
using Amuse.UI.Models.FeatureExtractor;
using Amuse.UI.Models.StableDiffusion;
using Amuse.UI.Models.Upscale;
using Amuse.UI.Services;
using Microsoft.Extensions.Logging;
using OnnxStack.FeatureExtractor.Pipelines;
using OnnxStack.StableDiffusion.Common;
using OnnxStack.StableDiffusion.Config;
using OnnxStack.StableDiffusion.Enums;
using OnnxStack.StableDiffusion.Models;
using OnnxStack.StableDiffusion.Pipelines;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;

namespace Amuse.UI.Views
{
    public class StableDiffusionViewBase : ViewBase
    {
        public StableDiffusionViewBase()
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                ModelCacheService = App.GetService<IModelCacheService>();
                ModeratorService = App.GetService<IModeratorService>();
                PreviewService = App.GetService<IPreviewService>();
            }

            ProgressCallback = CreateProgressCallback();
            LoadPipelineCommand = new AsyncRelayCommand(LoadPipelineAsync, CanLoadPipeline);
            UnloadPipelineCommand = new AsyncRelayCommand(UnloadPipelineAsync, CanUnloadPipeline);
        }

        public static readonly DependencyProperty SelectedBaseModelProperty =
            DependencyProperty.Register(nameof(SelectedBaseModel), typeof(StableDiffusionModelSetViewModel), typeof(StableDiffusionViewBase));

        public static readonly DependencyProperty SelectedControlNetModelProperty =
            DependencyProperty.Register(nameof(SelectedControlNetModel), typeof(ControlNetModelSetViewModel), typeof(StableDiffusionViewBase));

        public static readonly DependencyProperty SelectedUpscaleModelProperty =
            DependencyProperty.Register(nameof(SelectedUpscaleModel), typeof(UpscaleModelSetViewModel), typeof(StableDiffusionViewBase));

        public static readonly DependencyProperty SelectedFeatureExtractorModelProperty =
            DependencyProperty.Register(nameof(SelectedFeatureExtractorModel), typeof(FeatureExtractorModelSetViewModel), typeof(StableDiffusionViewBase));

        public static readonly DependencyProperty SelectedVariantProperty =
            DependencyProperty.Register(nameof(SelectedVariant), typeof(string), typeof(FeatureExtractorModelSetViewModel));

        public static readonly DependencyProperty CurrentPipelineProperty =
            DependencyProperty.Register(nameof(CurrentPipeline), typeof(StableDiffusionPipelineModel), typeof(StableDiffusionViewBase));

        public static readonly DependencyProperty IsUpscalerEnabledProperty =
            DependencyProperty.Register(nameof(IsUpscalerEnabled), typeof(bool), typeof(StableDiffusionViewBase));

        public static readonly DependencyProperty IsControlNetEnabledProperty =
            DependencyProperty.Register(nameof(IsControlNetEnabled), typeof(bool), typeof(StableDiffusionViewBase));

        public static readonly DependencyProperty IsFeatureExtractorEnabledProperty =
            DependencyProperty.Register(nameof(IsFeatureExtractorEnabled), typeof(bool), typeof(StableDiffusionViewBase));

        public static readonly DependencyProperty IsPipelineLoadingProperty =
            DependencyProperty.Register(nameof(IsPipelineLoading), typeof(bool), typeof(StableDiffusionViewBase));

        public static readonly DependencyProperty MemoryInfoProperty =
            DependencyProperty.Register(nameof(MemoryInfo), typeof(MemoryInfoModel), typeof(StableDiffusionViewBase));


        /// <summary>
        /// Gets or sets the selected model.
        /// </summary>
        public StableDiffusionModelSetViewModel SelectedBaseModel
        {
            get { return (StableDiffusionModelSetViewModel)GetValue(SelectedBaseModelProperty); }
            set { SetValue(SelectedBaseModelProperty, value); }
        }

        /// <summary>
        /// Gets or sets the selected ControlNet model.
        /// </summary>
        public ControlNetModelSetViewModel SelectedControlNetModel
        {
            get { return (ControlNetModelSetViewModel)GetValue(SelectedControlNetModelProperty); }
            set { SetValue(SelectedControlNetModelProperty, value); }
        }

        /// <summary>
        /// Gets or sets the selected upscale model.
        /// </summary>
        public UpscaleModelSetViewModel SelectedUpscaleModel
        {
            get { return (UpscaleModelSetViewModel)GetValue(SelectedUpscaleModelProperty); }
            set { SetValue(SelectedUpscaleModelProperty, value); }
        }

        /// <summary>
        /// Gets or sets the selected feature extractor model.
        /// </summary>
        public FeatureExtractorModelSetViewModel SelectedFeatureExtractorModel
        {
            get { return (FeatureExtractorModelSetViewModel)GetValue(SelectedFeatureExtractorModelProperty); }
            set { SetValue(SelectedFeatureExtractorModelProperty, value); }
        }

        /// <summary>
        /// Gets or sets the selected variant.
        /// </summary>
        public string SelectedVariant
        {
            get { return (string)GetValue(SelectedVariantProperty); }
            set { SetValue(SelectedVariantProperty, value); }
        }

        /// <summary>
        /// Gets or sets the current pipeline.
        /// </summary>
        public StableDiffusionPipelineModel CurrentPipeline
        {
            get { return (StableDiffusionPipelineModel)GetValue(CurrentPipelineProperty); }
            set { SetValue(CurrentPipelineProperty, value); }
        }

        public MemoryInfoModel MemoryInfo
        {
            get { return (MemoryInfoModel)GetValue(MemoryInfoProperty); }
            set { SetValue(MemoryInfoProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether upscale is enabled.
        /// </summary>
        public bool IsUpscalerEnabled
        {
            get { return (bool)GetValue(IsUpscalerEnabledProperty); }
            set { SetValue(IsUpscalerEnabledProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether control net is enabled.
        /// </summary>
        public bool IsControlNetEnabled
        {
            get { return (bool)GetValue(IsControlNetEnabledProperty); }
            set { SetValue(IsControlNetEnabledProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether feature extractor is enabled.
        /// </summary>
        public bool IsFeatureExtractorEnabled
        {
            get { return (bool)GetValue(IsFeatureExtractorEnabledProperty); }
            set { SetValue(IsFeatureExtractorEnabledProperty, value); }
        }


        /// <summary>
        /// Gets or sets a value indicating whether the pipeline is loading.
        /// </summary>
        public bool IsPipelineLoading
        {
            get { return (bool)GetValue(IsPipelineLoadingProperty); }
            set { SetValue(IsPipelineLoadingProperty, value); }
        }

        /// <summary>
        /// Gets the model cache service.
        /// </summary>
        /// <value>
        public IModelCacheService ModelCacheService { get; }


        /// <summary>
        /// Gets the moderator service.
        /// </summary>
        public IModeratorService ModeratorService { get; }

        /// <summary>
        /// Gets the preview service.
        /// </summary>
        public IPreviewService PreviewService { get; }

        /// <summary>
        /// Gets the load pipeline command.
        /// </summary>
        public AsyncRelayCommand LoadPipelineCommand { get; }

        /// <summary>
        /// Gets the unload pipeline command.
        /// </summary>
        public AsyncRelayCommand UnloadPipelineCommand { get; }

        /// <summary>
        /// Gets a value indicating whether this view supports Upscale.
        /// </summary>
        public virtual bool IsUpscalerSupported { get; }

        /// <summary>
        /// Gets a value indicating whether this view supports ControlNet.
        /// </summary>
        public virtual bool IsControlNetSupported { get; }

        /// <summary>
        /// Gets a value indicating whether this view supports FeatureExtractors.
        /// </summary>
        public virtual bool IsFeatureExtractorSupported { get; }

        /// <summary>
        /// Gets the diffusion progress callback.
        /// </summary>
        protected IProgress<DiffusionProgress> ProgressCallback { get; }


        /// <summary>
        /// Loads the pipeline.
        /// </summary>
        protected virtual async Task LoadPipelineAsync()
        {
            try
            {
                IsPipelineLoading = true;
                UpdateProgress("Loading Pipeline...");

                if (!IsUpscalerEnabled)
                    await UnloadUpscalerAsync();
                if (!IsControlNetEnabled)
                    await UnloadControlNetAsync();
                if (!IsFeatureExtractorEnabled)
                    await UnloadFeatureExtractorAsync();
                if (!ModeratorService.IsContentFilterEnabled)
                    await UnloadContentFilterAsync();

                if (SelectedBaseModel.Variant != SelectedVariant)
                {
                    SelectedBaseModel.Variant = SelectedVariant;
                    if (SelectedBaseModel.IsLoaded)
                        await UnloadBaseModelAsync();
                }

                var pipeline = new StableDiffusionPipelineModel(
                    Settings,
                    SelectedBaseModel,
                    SelectedControlNetModel,
                    SelectedUpscaleModel,
                    SelectedFeatureExtractorModel,
                    ModeratorService.ContentFilterModel);

                SetMemoryInfo(pipeline);
                CurrentPipeline = pipeline;

                await Task.WhenAll(
                    LoadBaseModelAsync(),
                    LoadControlNetAsync(),
                    LoadUpscalerAsync(),
                    LoadFeatureExtractorAsync(),
                    LoadContentFilterAsync(),
                    LoadPreviewAsync());
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("LoadPipeline was canceled");
                await UnloadPipelineAsync();
            }
            catch (Exception ex)
            {
                await UnloadPipelineAsync();

                UnrecoverableException.ThrowIf(ex);
                Logger.LogError("Error during LoadPipeline\n{ex}", ex);
                await DialogService.ShowErrorMessageAsync("Load Pipeline Error", ex.Message);
            }

            UpdateProgress(string.Empty);
            IsPipelineLoading = false;
        }


        /// <summary>
        /// Determines whether the pipeline can be loaded.
        /// </summary>
        protected bool CanLoadPipeline()
        {
            if (SelectedBaseModel is null)
                return false;

            var loaded = SelectedBaseModel.IsLoaded;
            if (IsFeatureExtractorEnabled)
            {
                if (SelectedFeatureExtractorModel is null)
                    return false;
                loaded = loaded && SelectedFeatureExtractorModel.IsLoaded;
            }

            if (IsControlNetEnabled)
            {
                if (SelectedControlNetModel is null)
                    return false;
                loaded = loaded && SelectedControlNetModel.IsLoaded;
            }

            if (IsUpscalerEnabled)
            {
                if (SelectedUpscaleModel is null)
                    return false;
                loaded = loaded && SelectedUpscaleModel.IsLoaded;
            }

            if (ModeratorService.IsContentFilterEnabled)
            {
                if (ModeratorService.ContentFilterModel is null)
                    return false;
                loaded = loaded && ModeratorService.ContentFilterModel.IsLoaded;
            }
     

            if (loaded)
            {
                // All selected models are Loaded
                if (CurrentPipeline is null)
                    return true;
                if (CurrentPipeline.BaseModel != SelectedBaseModel)
                    return true;
                if (CurrentPipeline.BaseModel.Variant != SelectedVariant)
                    return true;
                if (CurrentPipeline?.UpscaleModel != SelectedUpscaleModel)
                    return true;
                if (CurrentPipeline?.ControlNetModel != SelectedControlNetModel)
                    return true;
                if (CurrentPipeline?.FeatureExtractorModel != SelectedFeatureExtractorModel)
                    return true;
                if (CurrentPipeline?.ContentFilterModel != ModeratorService.ContentFilterModel)
                    return true;

                return !CurrentPipeline.IsLoaded;
            }
            return !loaded;
        }


        /// <summary>
        /// Unloads the pipeline.
        /// </summary>
        protected async Task UnloadPipelineAsync()
        {
            IsPipelineLoading = true;
            await UnloadModels();
            CurrentPipeline = null;
            SelectedBaseModel = null;
            SelectedVariant = null;
            SelectedControlNetModel = null;
            SelectedUpscaleModel = null;
            SelectedFeatureExtractorModel = null;
            IsUpscalerEnabled = false;
            IsControlNetEnabled = false;
            IsFeatureExtractorEnabled = false;
            IsPipelineLoading = false;
        }


        /// <summary>
        /// Unloads the models.
        /// </summary>
        protected async Task UnloadModels()
        {
            await Task.WhenAll(
                  UnloadBaseModelAsync(),
                  UnloadFeatureExtractorAsync(),
                  UnloadControlNetAsync(),
                  UnloadUpscalerAsync(),
                  UnloadContentFilterAsync(),
                  UnloadPreviewAsync());
        }

        /// <summary>
        /// Determines whether the pipeline can be unloaded.
        /// </summary>
        private bool CanUnloadPipeline()
        {
            return CurrentPipeline?.BaseModel.IsLoaded == true
                || CurrentPipeline?.UpscaleModel?.IsLoaded == true
                || CurrentPipeline?.ControlNetModel?.IsLoaded == true
                || CurrentPipeline?.FeatureExtractorModel?.IsLoaded == true;
        }


        /// <summary>
        /// Loads the stable diffusion model.
        /// </summary>
        /// <param name="stableDiffusion">The stable diffusion.</param>
        /// <param name="isControlNet">if set to <c>true</c> [is control net].</param>
        protected async Task<IPipeline> LoadBaseModelAsync()
        {
            return await ModelCacheService.LoadModelAsync(CurrentPipeline.BaseModel, CurrentPipeline.IsControlNetEnabled);
        }


        /// <summary>
        /// Unloads the stable diffusion model.
        /// </summary>
        protected async Task UnloadBaseModelAsync()
        {
            if (CurrentPipeline?.BaseModel is null)
                return;

            await ModelCacheService.UnloadModelAsync(CurrentPipeline.BaseModel);
        }


        /// <summary>
        /// Loads the feature extractor.
        /// </summary>
        /// <param name="featureExtractor">The feature extractor.</param>
        protected async Task<FeatureExtractorPipeline> LoadFeatureExtractorAsync()
        {
            if (CurrentPipeline?.FeatureExtractorModel is null)
                return default;

            return await ModelCacheService.LoadModelAsync(CurrentPipeline.FeatureExtractorModel);
        }


        /// <summary>
        /// Unloads the feature extractors.
        /// </summary>
        protected async Task UnloadFeatureExtractorAsync()
        {
            if (CurrentPipeline?.FeatureExtractorModel is null)
                return;

            await ModelCacheService.UnloadModelAsync(CurrentPipeline.FeatureExtractorModel);
        }


        /// <summary>
        /// Loads the control net.
        /// </summary>
        /// <param name="controlNet">The control net.</param>
        protected async Task<ControlNetModel> LoadControlNetAsync()
        {
            if (CurrentPipeline?.ControlNetModel is null)
                return default;

            return await ModelCacheService.LoadModelAsync(CurrentPipeline.ControlNetModel);
        }


        /// <summary>
        /// Unloads the control nets.
        /// </summary>
        protected async Task UnloadControlNetAsync()
        {
            if (CurrentPipeline?.ControlNetModel is null)
                return;

            await ModelCacheService.UnloadModelAsync(CurrentPipeline.ControlNetModel);
        }


        /// <summary>
        /// Loads the upscaler.
        /// </summary>
        /// <param name="upscaler">The upscaler.</param>
        protected async Task<ImageUpscalePipeline> LoadUpscalerAsync()
        {
            if (CurrentPipeline.UpscaleModel is null)
                return default;

            return await ModelCacheService.LoadModelAsync(CurrentPipeline.UpscaleModel);
        }


        /// <summary>
        /// Unloads the upscalers.
        /// </summary>
        protected async Task UnloadUpscalerAsync()
        {
            if (CurrentPipeline?.UpscaleModel is null)
                return;

            await ModelCacheService.UnloadModelAsync(CurrentPipeline.UpscaleModel);
        }


        /// <summary>
        /// Loads the ContentFilter.
        /// </summary>
        protected async Task<ContentFilterPipeline> LoadContentFilterAsync()
        {
            if (CurrentPipeline?.ContentFilterModel is null)
                return default;

            return await ModelCacheService.LoadModelAsync(CurrentPipeline?.ContentFilterModel);
        }


        /// <summary>
        /// Unloads the ContentFilter.
        /// </summary>
        protected async Task UnloadContentFilterAsync()
        {
            if (CurrentPipeline?.ContentFilterModel is null)
                return;

            await ModelCacheService.UnloadModelAsync(CurrentPipeline?.ContentFilterModel);
        }


        /// <summary>
        /// Load preview service
        /// </summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
        protected async Task LoadPreviewAsync()
        {
            await PreviewService.LoadAsync(SelectedBaseModel.ModelSet);
        }


        /// <summary>
        /// Unload preview service
        /// </summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
        protected async Task UnloadPreviewAsync()
        {
            await PreviewService.UnloadAsync();
        }


        /// <summary>
        /// Updates the progress.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <returns></returns>
        protected virtual Task UpdateProgress(DiffusionProgress progress)
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// Updates the progress Image.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <returns>Task.</returns>
        protected virtual Task UpdateProgressImage(DiffusionProgress progress)
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// Creates the progress callback.
        /// </summary>
        /// <returns></returns>
        protected virtual Progress<DiffusionProgress> CreateProgressCallback()
        {
            return new Progress<DiffusionProgress>(async (progress) =>
            {
                var stepTensor = progress.StepTensor?.ToDenseTensor();
                var batchTensor = progress.BatchTensor?.ToDenseTensor();

                if (CancelationTokenSource.IsCancellationRequested)
                    return;

                var result = progress with
                {
                    StepTensor = stepTensor,
                    BatchTensor = batchTensor
                };

                await App.UIInvokeAsync(() => UpdateProgress(result));
                await App.UIInvokeAsync(() => UpdateProgressImage(result));
            });
        }


        /// <summary>
        /// Called when the DefaultExecutionDevice has changed.
        /// </summary>
        /// <returns>Task.</returns>
        protected override Task OnDefaultExecutionDeviceChanged()
        {
            SetMemoryInfo(CurrentPipeline);
            return base.OnDefaultExecutionDeviceChanged();
        }


        /// <summary>
        /// Sets the memory information.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        private void SetMemoryInfo(StableDiffusionPipelineModel pipeline)
        {
            if (pipeline != null)
            {
                MemoryInfo = new MemoryInfoModel(pipeline, Settings.DefaultExecutionDevice.MemoryGB, Settings.DefaultExecutionDevice.MemorySharedGB);
            }
        }
    }
}
