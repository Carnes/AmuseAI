using Amuse.UI.Models;
using Amuse.UI.Models.FeatureExtractor;
using Amuse.UI.Models.StableDiffusion;
using Amuse.UI.Models.Upscale;
using Amuse.UI.Pipelines;
using Microsoft.Extensions.Logging;
using OnnxStack.Core;
using OnnxStack.Core.Config;
using OnnxStack.Core.Model;
using OnnxStack.FeatureExtractor.Pipelines;
using OnnxStack.StableDiffusion.Enums;
using OnnxStack.StableDiffusion.Models;
using OnnxStack.StableDiffusion.Pipelines;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Amuse.UI.Services
{
    public class ModelCacheService : IModelCacheService
    {
        private readonly AmuseSettings _settings;
        private readonly ILogger<ModelCacheService> _logger;
        private readonly IProviderService _providerService;
        private readonly Dictionary<UpscaleModelSetViewModel, ImageUpscalePipeline> _upscalePipelines;
        private readonly Dictionary<ControlNetModelSetViewModel, ControlNetModel> _controlnetPipelines;
        private readonly Dictionary<StableDiffusionModelSetViewModel, IPipeline> _stableDiffusionPipelines;
        private readonly Dictionary<FeatureExtractorModelSetViewModel, FeatureExtractorPipeline> _featureExtractorPipelines;
        private readonly Dictionary<ContentFilterModelSetViewModel, ContentFilterPipeline> _filterPipelines;

        public ModelCacheService(AmuseSettings settings, IProviderService providerService, ILogger<ModelCacheService> logger)
        {
            _logger = logger;
            _settings = settings;
            _providerService = providerService;
            _upscalePipelines = new Dictionary<UpscaleModelSetViewModel, ImageUpscalePipeline>();
            _controlnetPipelines = new Dictionary<ControlNetModelSetViewModel, ControlNetModel>();
            _stableDiffusionPipelines = new Dictionary<StableDiffusionModelSetViewModel, IPipeline>();
            _featureExtractorPipelines = new Dictionary<FeatureExtractorModelSetViewModel, FeatureExtractorPipeline>();
            _filterPipelines = new Dictionary<ContentFilterModelSetViewModel, ContentFilterPipeline>();
        }


        public async Task<IPipeline> LoadModelAsync(StableDiffusionModelSetViewModel model, bool isControlNet)
        {
            try
            {
                if (model == null)
                    return null;

                if (_stableDiffusionPipelines.TryGetValue(model, out var pipeline))
                    return pipeline;

                if (_settings.ModelCacheMode == ModelCacheMode.Single)
                {
                    foreach (var key in _stableDiffusionPipelines.Keys.ToArray())
                    {
                        await UnloadModelAsync(key);
                    }
                }

                model.IsLoading = true;
                model.IsLoaded = false;
                pipeline = CreatePipeline(model);
                //await pipeline.LoadAsync(unetMode);
                _stableDiffusionPipelines.Add(model, pipeline);
                model.IsLoaded = true;
                model.IsLoading = false;
                return pipeline;
            }
            catch (Exception)
            {
                model.IsLoaded = false;
                model.IsLoading = false;
                throw;
            }
        }


        public async Task<ControlNetModel> LoadModelAsync(ControlNetModelSetViewModel model)
        {
            try
            {
                if (model == null)
                    return null;

                if (_controlnetPipelines.TryGetValue(model, out var pipeline))
                    return pipeline;

                if (_settings.ModelCacheMode == ModelCacheMode.Single)
                {
                    foreach (var key in _controlnetPipelines.Keys.ToArray())
                    {
                        await UnloadModelAsync(key);
                    }
                }
                model.IsLoading = true;
                model.IsLoaded = false;

                pipeline = CreatePipeline(model);
                //await pipeline.LoadAsync();
                _controlnetPipelines.Add(model, pipeline);

                model.IsLoaded = true;
                model.IsLoading = false;
                return pipeline;
            }
            catch (Exception)
            {
                model.IsLoaded = false;
                model.IsLoading = false;
                throw;
            }
        }


        public async Task<ImageUpscalePipeline> LoadModelAsync(UpscaleModelSetViewModel model)
        {
            try
            {
                if (model == null)
                    return null;

                if (_upscalePipelines.TryGetValue(model, out var pipeline))
                    return pipeline;

                if (_settings.ModelCacheMode == ModelCacheMode.Single)
                {
                    foreach (var key in _upscalePipelines.Keys.ToArray())
                    {
                        await UnloadModelAsync(key);
                    }
                }

                model.IsLoading = true;
                model.IsLoaded = false;

                pipeline = CreatePipeline(model);
                // await pipeline.LoadAsync();
                _upscalePipelines.Add(model, pipeline);

                model.IsLoaded = true;
                model.IsLoading = false;
                return pipeline;
            }
            catch (Exception)
            {
                model.IsLoaded = false;
                model.IsLoading = false;
                throw;
            }
        }


        public async Task<FeatureExtractorPipeline> LoadModelAsync(FeatureExtractorModelSetViewModel model)
        {
            try
            {
                if (model == null)
                    return null;

                if (_featureExtractorPipelines.TryGetValue(model, out var pipeline))
                    return pipeline;

                if (_settings.ModelCacheMode == ModelCacheMode.Single)
                {
                    foreach (var key in _featureExtractorPipelines.Keys.ToArray())
                    {
                        await UnloadModelAsync(key);
                    }
                }

                model.IsLoading = true;
                model.IsLoaded = false;

                pipeline = CreatePipeline(model);
                //await pipeline.LoadAsync();
                _featureExtractorPipelines.Add(model, pipeline);

                model.IsLoaded = true;
                model.IsLoading = false;
                return pipeline;
            }
            catch (Exception)
            {
                model.IsLoaded = false;
                model.IsLoading = false;
                throw;
            }
        }


        public async Task<ContentFilterPipeline> LoadModelAsync(ContentFilterModelSetViewModel model)
        {
            try
            {
                if (model == null)
                    return null;

                if (_filterPipelines.TryGetValue(model, out var pipeline))
                    return pipeline;

                foreach (var key in _filterPipelines.Keys.ToArray())
                {
                    await UnloadModelAsync(key);
                }

                model.IsLoading = true;
                model.IsLoaded = false;

                pipeline = CreatePipeline(model);
                // await pipeline.LoadAsync();
                _filterPipelines.Add(model, pipeline);

                model.IsLoaded = true;
                model.IsLoading = false;
                return pipeline;
            }
            catch (Exception)
            {
                model.IsLoaded = false;
                model.IsLoading = false;
                throw;
            }
        }


        public async Task<bool> UnloadModelAsync(StableDiffusionModelSetViewModel model)
        {
            if (_stableDiffusionPipelines.Remove(model, out var pipeline))
            {
                model.IsLoading = true;
                await pipeline?.UnloadAsync();
                model.IsLoaded = false;
                model.IsLoading = false;
            }
            return true;
        }


        public async Task<bool> UnloadModelAsync(ControlNetModelSetViewModel model)
        {
            if (_controlnetPipelines.Remove(model, out var pipeline))
            {
                model.IsLoading = true;
                await pipeline?.UnloadAsync();
                model.IsLoaded = false;
                model.IsLoading = false;
            }
            return true;
        }


        public async Task<bool> UnloadModelAsync(UpscaleModelSetViewModel model)
        {
            if (_upscalePipelines.Remove(model, out var pipeline))
            {
                model.IsLoading = true;
                await pipeline?.UnloadAsync();
                model.IsLoaded = false;
                model.IsLoading = false;
            }
            return true;
        }


        public async Task<bool> UnloadModelAsync(FeatureExtractorModelSetViewModel model)
        {
            if (_featureExtractorPipelines.Remove(model, out var pipeline))
            {
                model.IsLoading = true;
                await pipeline?.UnloadAsync();
                model.IsLoaded = false;
                model.IsLoading = false;
            }
            return true;
        }


        public async Task<bool> UnloadModelAsync(ContentFilterModelSetViewModel model)
        {
            if (_filterPipelines.Remove(model, out var pipeline))
            {
                model.IsLoading = true;
                await pipeline?.UnloadAsync();
                model.IsLoaded = false;
                model.IsLoading = false;
            }
            return true;
        }


        public bool IsModelLoaded(StableDiffusionModelSetViewModel model)
        {
            return model is not null && _stableDiffusionPipelines.TryGetValue(model, out _);
        }


        public bool IsModelLoaded(ControlNetModelSetViewModel model)
        {
            return model is not null && _controlnetPipelines.TryGetValue(model, out _);
        }


        public bool IsModelLoaded(UpscaleModelSetViewModel model)
        {
            return model is not null && _upscalePipelines.TryGetValue(model, out _);
        }


        public bool IsModelLoaded(FeatureExtractorModelSetViewModel model)
        {
            return model is not null && _featureExtractorPipelines.TryGetValue(model, out _);
        }


        public bool IsModelLoaded(ContentFilterModelSetViewModel model)
        {
            return model is not null && _filterPipelines.TryGetValue(model, out _);
        }


        private IPipeline CreatePipeline(StableDiffusionModelSetViewModel model)
        {
            var provider = _providerService.GetProvider(model.ModelSet.ExecutionProvider, model.ModelSet.DeviceId);
            var modelConfig = model.ModelSet.ToModelConfig(provider);
            var textEncoderProvider = GetProvider(model.ModelSet.TextEncoderConfig, model);
            var textEncoder2Provider = GetProvider(model.ModelSet.TextEncoder2Config, model);
            var textEncoder3Provider = GetProvider(model.ModelSet.TextEncoder3Config, model);
            var unetProvider = GetProvider(model.ModelSet.UnetConfig, model);
            var unet2Provider = GetProvider(model.ModelSet.Unet2Config, model);
            var controlNetUnetProvider = GetProvider(model.ModelSet.ControlNetUnetConfig, model);
            var vaeEncoderProvider = GetProvider(model.ModelSet.VaeEncoderConfig, model);
            var vaeDecoderProvider = GetProvider(model.ModelSet.VaeDecoderConfig, model);
            var resampleModelProvider = GetProvider(model.ModelSet.ResampleModelConfig, model);
            var flowEstimationProvider = GetProvider(model.ModelSet.FlowEstimationConfig, model);
            var modelset = modelConfig with
            {
                TextEncoderConfig = SetVariantModelPath(model.ModelSet.TextEncoderConfig?.ToModelConfig(textEncoderProvider), model.Variant),
                TextEncoder2Config = SetVariantModelPath(model.ModelSet.TextEncoder2Config?.ToModelConfig(textEncoder2Provider), model.Variant),
                TextEncoder3Config = SetVariantModelPath(model.ModelSet.TextEncoder3Config?.ToModelConfig(textEncoder3Provider), model.Variant),
                UnetConfig = SetVariantModelPath(model.ModelSet.UnetConfig?.ToModelConfig(unetProvider), model.Variant),
                Unet2Config = SetVariantModelPath(model.ModelSet.Unet2Config?.ToModelConfig(unet2Provider), model.Variant),
                ControlNetUnetConfig = SetVariantModelPath(model.ModelSet.ControlNetUnetConfig?.ToModelConfig(controlNetUnetProvider), model.Variant),
                VaeEncoderConfig = SetVariantModelPath(model.ModelSet.VaeEncoderConfig?.ToModelConfig(vaeEncoderProvider), model.Variant),
                VaeDecoderConfig = SetVariantModelPath(model.ModelSet.VaeDecoderConfig?.ToModelConfig(vaeDecoderProvider), model.Variant),
                ResampleModelConfig = SetVariantModelPath(model.ModelSet.ResampleModelConfig?.ToModelConfig(resampleModelProvider), model.Variant),
                FlowEstimationConfig = SetVariantModelPath(model.ModelSet.FlowEstimationConfig?.ToModelConfig(flowEstimationProvider), model.Variant),
            };


            return modelset.PipelineType switch
            {
                PipelineType.StableDiffusion => StableDiffusionPipeline.CreatePipeline(modelset, _logger),
                PipelineType.StableDiffusion2 => StableDiffusion2Pipeline.CreatePipeline(modelset, _logger),
                PipelineType.StableDiffusionXL => StableDiffusionXLPipeline.CreatePipeline(modelset, _logger),
                PipelineType.LatentConsistency => LatentConsistencyPipeline.CreatePipeline(modelset, _logger),
                PipelineType.StableCascade => StableCascadePipeline.CreatePipeline(modelset, _logger),
                PipelineType.StableDiffusion3 => StableDiffusion3Pipeline.CreatePipeline(modelset, _logger),
                PipelineType.Flux => FluxPipeline.CreatePipeline(modelset, _logger),
                PipelineType.Locomotion => LocomotionPipeline.CreatePipeline(modelset, _logger),
                _ => throw new NotSupportedException()
            };
        }


        private ImageUpscalePipeline CreatePipeline(UpscaleModelSetViewModel model)
        {
            var provider = _providerService.GetProvider(model.ModelSet.ExecutionProvider, model.ModelSet.DeviceId);
            var modelConfig = model.ModelSet.ToModelConfig(provider);
            var modelset = SetVariantModelPath(modelConfig, model.Variant);
            return ImageUpscalePipeline.CreatePipeline(modelset, _logger);
        }


        private FeatureExtractorPipeline CreatePipeline(FeatureExtractorModelSetViewModel model)
        {
            // InternalFeatureExtractorPipeline
            if (model.Id == Utils.InternalExtractorCanny)
                return new InternalFeatureExtractorPipeline("Canny", default, _logger);
            else if (model.Id == Utils.InternalExtractorSoftEdge)
                return new InternalFeatureExtractorPipeline("SoftEdge", default, _logger);

            var provider = _providerService.GetProvider(model.ModelSet.ExecutionProvider, model.ModelSet.DeviceId);
            var modelConfig = model.ModelSet.ToModelConfig(provider);
            var modelset = SetVariantModelPath(modelConfig, model.Variant);
            return FeatureExtractorPipeline.CreatePipeline(modelset, _logger);
        }


        private ContentFilterPipeline CreatePipeline(ContentFilterModelSetViewModel model)
        {
            var provider = _providerService.GetProvider(model.ModelSet.ExecutionProvider, model.ModelSet.DeviceId);
            var modelConfig = model.ModelSet.ToModelConfig(provider);
            var modelset = SetVariantModelPath(modelConfig, model.Variant);
            return ContentFilterPipeline.CreatePipeline(modelset, _logger);
        }


        private ControlNetModel CreatePipeline(ControlNetModelSetViewModel model)
        {
            var provider = _providerService.GetProvider(model.ModelSet.ExecutionProvider, model.ModelSet.DeviceId);
            var modelConfig = model.ModelSet.ToModelConfig(provider);
            return new ControlNetModel(modelConfig);
        }


        private OnnxExecutionProvider GetProvider(OnnxModelJson modelJson, StableDiffusionModelSetViewModel model)
        {
            if (modelJson is null)
                return null;

            var deviceId = modelJson.DeviceId ?? model.ModelSet.DeviceId;
            var executionProvider = modelJson.ExecutionProvider ?? model.ModelSet.ExecutionProvider;
            return _providerService.GetProvider(executionProvider, deviceId);
        }


        private static T SetVariantModelPath<T>(T modelConfig, string variant) where T : OnnxModelConfig
        {
            if (modelConfig is null)
                return modelConfig;
            if (string.IsNullOrEmpty(variant))
                return modelConfig;

            var variantModel = GetVariantPath(modelConfig.OnnxModelPath, variant);
            if (!File.Exists(variantModel))
                return modelConfig;

            modelConfig.OnnxModelPath = variantModel;
            return modelConfig;
        }


        private static string GetVariantPath(string modelPath, string variant)
        {
            return Path.Combine(Path.GetDirectoryName(modelPath), variant, Path.GetFileName(modelPath));
        }
    }
}
