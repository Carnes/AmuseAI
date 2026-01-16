using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amuse.UI.Core.Models;
using Amuse.UI.Models;
using Amuse.UI.Models.StableDiffusion;
using Amuse.UI.Models.Upscale;
using Amuse.UI.Services;
using Microsoft.Extensions.Logging;
using OnnxStack.Core.Image;
using OnnxStack.ImageUpscaler.Common;
using OnnxStack.StableDiffusion.Common;
using OnnxStack.StableDiffusion.Config;
using OnnxStack.StableDiffusion.Enums;

namespace Amuse.UI.Core.Services
{
    /// <summary>
    /// Unified generation service for all frontends.
    /// Wraps the existing model cache and pipeline infrastructure.
    /// </summary>
    public class GenerationService : IGenerationService
    {
        private readonly ILogger<GenerationService> _logger;
        private readonly AmuseSettings _settings;
        private readonly IModelCacheService _modelCacheService;

        public GenerationService(
            AmuseSettings settings,
            IModelCacheService modelCacheService,
            ILogger<GenerationService> logger)
        {
            _logger = logger;
            _settings = settings;
            _modelCacheService = modelCacheService;
        }

        public async Task<GenerationJobResult> GenerateTextToImageAsync(
            TextToImageParameters parameters,
            IProgress<DiffusionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var timestamp = Stopwatch.GetTimestamp();

            // Find model by name
            var modelViewModel = FindModelByName(parameters.ModelName);
            if (modelViewModel == null)
                throw new ArgumentException($"Model '{parameters.ModelName}' not found");

            _logger.LogInformation("[GenerationService] Loading model '{ModelName}'...", parameters.ModelName);

            // Load pipeline
            var pipeline = await _modelCacheService.LoadModelAsync(modelViewModel, false);

            // Build scheduler options
            var schedulerOptions = BuildSchedulerOptions(parameters, modelViewModel);

            // Build generate options
            var generateOptions = new GenerateOptions
            {
                Prompt = parameters.Prompt,
                NegativePrompt = parameters.NegativePrompt ?? string.Empty,
                SchedulerOptions = schedulerOptions,
                Diffuser = DiffuserType.TextToImage
            };

            _logger.LogInformation("[GenerationService] Generating image with seed {Seed}...", schedulerOptions.Seed);

            // Execute generation
            var result = await Task.Run(() =>
                pipeline.GenerateAsync(generateOptions, progress, cancellationToken),
                cancellationToken);

            // Convert to bytes
            var imageBytes = await ConvertOnnxImageToBytesAsync(result, cancellationToken);

            var elapsed = Stopwatch.GetElapsedTime(timestamp).TotalSeconds;
            _logger.LogInformation("[GenerationService] Generation completed in {Elapsed:F2}s", elapsed);

            return new GenerationJobResult
            {
                ImageData = imageBytes,
                ImageFormat = "image/png",
                Width = schedulerOptions.Width,
                Height = schedulerOptions.Height,
                Seed = schedulerOptions.Seed,
                InferenceSteps = schedulerOptions.InferenceSteps,
                GuidanceScale = schedulerOptions.GuidanceScale,
                ElapsedSeconds = elapsed,
                ModelName = parameters.ModelName
            };
        }

        public async Task<GenerationJobResult> GenerateImageToImageAsync(
            ImageToImageParameters parameters,
            IProgress<DiffusionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var timestamp = Stopwatch.GetTimestamp();

            // Find model by name
            var modelViewModel = FindModelByName(parameters.ModelName);
            if (modelViewModel == null)
                throw new ArgumentException($"Model '{parameters.ModelName}' not found");

            // Load pipeline
            var pipeline = await _modelCacheService.LoadModelAsync(modelViewModel, false);

            // Build scheduler options
            var schedulerOptions = BuildSchedulerOptions(parameters, modelViewModel);
            schedulerOptions.Strength = parameters.Strength;

            // Convert input image bytes to OnnxImage
            var inputImage = await OnnxImage.FromBytesAsync(parameters.InputImage);

            // Build generate options
            var generateOptions = new GenerateOptions
            {
                Prompt = parameters.Prompt,
                NegativePrompt = parameters.NegativePrompt ?? string.Empty,
                SchedulerOptions = schedulerOptions,
                Diffuser = DiffuserType.ImageToImage,
                InputImage = inputImage
            };

            _logger.LogInformation("[GenerationService] Generating image-to-image with seed {Seed}...", schedulerOptions.Seed);

            // Execute generation
            var result = await Task.Run(() =>
                pipeline.GenerateAsync(generateOptions, progress, cancellationToken),
                cancellationToken);

            // Convert to bytes
            var imageBytes = await ConvertOnnxImageToBytesAsync(result, cancellationToken);

            var elapsed = Stopwatch.GetElapsedTime(timestamp).TotalSeconds;

            return new GenerationJobResult
            {
                ImageData = imageBytes,
                ImageFormat = "image/png",
                Width = schedulerOptions.Width,
                Height = schedulerOptions.Height,
                Seed = schedulerOptions.Seed,
                InferenceSteps = schedulerOptions.InferenceSteps,
                GuidanceScale = schedulerOptions.GuidanceScale,
                ElapsedSeconds = elapsed,
                ModelName = parameters.ModelName
            };
        }

        public async Task<GenerationJobResult> UpscaleImageAsync(
            UpscaleParameters parameters,
            IProgress<DiffusionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var timestamp = Stopwatch.GetTimestamp();

            // Find upscale model by name
            var modelViewModel = FindUpscaleModelByName(parameters.ModelName);
            if (modelViewModel == null)
                throw new ArgumentException($"Upscale model '{parameters.ModelName}' not found");

            // Load pipeline
            var pipeline = await _modelCacheService.LoadModelAsync(modelViewModel);

            // Convert input image bytes to OnnxImage
            var inputImage = await OnnxImage.FromBytesAsync(parameters.InputImage);

            var options = new UpscaleOptions(
                modelViewModel.ModelSet.TileMode,
                modelViewModel.ModelSet.TileSize,
                modelViewModel.ModelSet.TileOverlap,
                false);

            _logger.LogInformation("[GenerationService] Upscaling image...");

            // Execute upscaling
            var result = await Task.Run(() =>
                pipeline.RunAsync(inputImage, options, cancellationToken),
                cancellationToken);

            // Convert to bytes
            var imageBytes = await ConvertOnnxImageToBytesAsync(result, cancellationToken);

            var elapsed = Stopwatch.GetElapsedTime(timestamp).TotalSeconds;

            return new GenerationJobResult
            {
                ImageData = imageBytes,
                ImageFormat = "image/png",
                Width = result.Width,
                Height = result.Height,
                ElapsedSeconds = elapsed,
                ModelName = parameters.ModelName
            };
        }

        public IReadOnlyList<ModelInfo> GetAvailableModels()
        {
            return _settings.StableDiffusionModelSets
                .Select(m => new ModelInfo
                {
                    Name = m.Name,
                    PipelineType = m.ModelSet.PipelineType.ToString(),
                    DefaultWidth = m.ModelSet.SampleSize,
                    DefaultHeight = m.ModelSet.SampleSize,
                    SupportedSchedulers = m.ModelSet.Schedulers?.Select(s => s.ToString()).ToList()
                        ?? GetDefaultSchedulers(m.ModelSet.PipelineType),
                    SupportedDiffusers = m.ModelSet.Diffusers?.Select(d => d.ToString()).ToList()
                        ?? new List<string> { "TextToImage", "ImageToImage" },
                    IsLoaded = _modelCacheService.IsModelLoaded(m)
                })
                .ToList();
        }

        public IReadOnlyList<ModelInfo> GetAvailableUpscaleModels()
        {
            return _settings.UpscaleModelSets
                .Select(m => new ModelInfo
                {
                    Name = m.Name,
                    PipelineType = "Upscale",
                    DefaultWidth = 0,
                    DefaultHeight = 0,
                    SupportedSchedulers = new List<string>(),
                    SupportedDiffusers = new List<string> { "Upscale" },
                    IsLoaded = _modelCacheService.IsModelLoaded(m)
                })
                .ToList();
        }

        public bool IsModelLoaded(string modelName)
        {
            var model = FindModelByName(modelName);
            return model != null && _modelCacheService.IsModelLoaded(model);
        }

        public async Task PreloadModelAsync(string modelName, CancellationToken cancellationToken = default)
        {
            var model = FindModelByName(modelName);
            if (model == null)
                throw new ArgumentException($"Model '{modelName}' not found");

            await _modelCacheService.LoadModelAsync(model, false);
        }

        private StableDiffusionModelSetViewModel FindModelByName(string modelName)
        {
            return _settings.StableDiffusionModelSets
                .FirstOrDefault(m => m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));
        }

        private UpscaleModelSetViewModel FindUpscaleModelByName(string modelName)
        {
            return _settings.UpscaleModelSets
                .FirstOrDefault(m => m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));
        }

        private SchedulerOptions BuildSchedulerOptions(TextToImageParameters parameters, StableDiffusionModelSetViewModel model)
        {
            var defaultOptions = model.ModelSet.SchedulerOptions ?? new SchedulerOptions();
            var pipelineDefaults = GetPipelineDefaults(model.ModelSet.PipelineType);

            var seed = parameters.Seed == 0 ? Random.Shared.Next() : parameters.Seed;

            var schedulerType = !string.IsNullOrEmpty(parameters.SchedulerType)
                ? Enum.Parse<SchedulerType>(parameters.SchedulerType, ignoreCase: true)
                : defaultOptions.SchedulerType;

            return new SchedulerOptions
            {
                Width = parameters.Width > 0 ? parameters.Width : (defaultOptions.Width > 0 ? defaultOptions.Width : pipelineDefaults.Width),
                Height = parameters.Height > 0 ? parameters.Height : (defaultOptions.Height > 0 ? defaultOptions.Height : pipelineDefaults.Height),
                Seed = seed,
                InferenceSteps = parameters.Steps > 0 ? parameters.Steps : (defaultOptions.InferenceSteps > 0 ? defaultOptions.InferenceSteps : pipelineDefaults.Steps),
                GuidanceScale = parameters.GuidanceScale > 0 ? parameters.GuidanceScale : (defaultOptions.GuidanceScale > 0 ? defaultOptions.GuidanceScale : pipelineDefaults.GuidanceScale),
                SchedulerType = schedulerType
            };
        }

        private static async Task<byte[]> ConvertOnnxImageToBytesAsync(OnnxImage image, CancellationToken cancellationToken)
        {
            // Save to temp file and read bytes
            var tempFile = Path.Combine(Path.GetTempPath(), $"amuse_api_{Guid.NewGuid():N}.png");
            try
            {
                await image.SaveAsync(tempFile);
                return await File.ReadAllBytesAsync(tempFile, cancellationToken);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        private static (int Width, int Height, int Steps, float GuidanceScale) GetPipelineDefaults(PipelineType pipelineType)
        {
            return pipelineType switch
            {
                PipelineType.StableDiffusion => (512, 512, 30, 7.5f),
                PipelineType.StableDiffusion2 => (768, 768, 30, 7.5f),
                PipelineType.StableDiffusionXL => (1024, 1024, 20, 5.0f),
                PipelineType.LatentConsistency => (512, 512, 6, 1.0f),
                PipelineType.StableCascade => (1024, 1024, 20, 4.0f),
                PipelineType.StableDiffusion3 => (1024, 1024, 28, 7.0f),
                PipelineType.Flux => (1024, 1024, 4, 0f),
                PipelineType.Locomotion => (512, 512, 8, 1.0f),
                _ => (512, 512, 30, 7.5f)
            };
        }

        private static IReadOnlyList<string> GetDefaultSchedulers(PipelineType pipelineType)
        {
            return pipelineType switch
            {
                PipelineType.StableDiffusion => new[] { "EulerAncestral", "Euler", "DDPM", "DDIM" },
                PipelineType.StableDiffusionXL => new[] { "EulerAncestral", "Euler", "DDPM" },
                PipelineType.LatentConsistency => new[] { "LCM" },
                PipelineType.Flux => new[] { "FlowMatchEulerDiscrete" },
                _ => new[] { "EulerAncestral", "Euler", "DDPM", "DDIM" }
            };
        }
    }
}
