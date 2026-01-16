using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amuse.UI.Core.Models;
using OnnxStack.StableDiffusion.Common;

namespace Amuse.UI.Core.Services
{
    /// <summary>
    /// Information about an available model.
    /// </summary>
    public class ModelInfo
    {
        public string Name { get; init; }
        public string PipelineType { get; init; }
        public int DefaultWidth { get; init; }
        public int DefaultHeight { get; init; }
        public IReadOnlyList<string> SupportedSchedulers { get; init; }
        public IReadOnlyList<string> SupportedDiffusers { get; init; }
        public bool IsLoaded { get; init; }
    }

    /// <summary>
    /// Parameters for text-to-image generation.
    /// </summary>
    public class TextToImageParameters
    {
        public string ModelName { get; init; }
        public string Prompt { get; init; }
        public string NegativePrompt { get; init; }
        public int Width { get; init; } = 512;
        public int Height { get; init; } = 512;
        public int Seed { get; init; } = 0;
        public int Steps { get; init; } = 30;
        public float GuidanceScale { get; init; } = 7.5f;
        public string SchedulerType { get; init; }
    }

    /// <summary>
    /// Parameters for image-to-image generation.
    /// </summary>
    public class ImageToImageParameters : TextToImageParameters
    {
        public byte[] InputImage { get; init; }
        public float Strength { get; init; } = 0.75f;
    }

    /// <summary>
    /// Parameters for image upscaling.
    /// </summary>
    public class UpscaleParameters
    {
        public string ModelName { get; init; }
        public byte[] InputImage { get; init; }
        public int ScaleFactor { get; init; } = 2;
    }

    /// <summary>
    /// Unified generation service interface.
    /// Provides generation capabilities to all frontends (UI, API, Discord, etc.).
    /// </summary>
    public interface IGenerationService
    {
        /// <summary>
        /// Generate an image from text prompt.
        /// </summary>
        Task<GenerationJobResult> GenerateTextToImageAsync(
            TextToImageParameters parameters,
            IProgress<DiffusionProgress> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate an image from another image with text guidance.
        /// </summary>
        Task<GenerationJobResult> GenerateImageToImageAsync(
            ImageToImageParameters parameters,
            IProgress<DiffusionProgress> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Upscale an image.
        /// </summary>
        Task<GenerationJobResult> UpscaleImageAsync(
            UpscaleParameters parameters,
            IProgress<DiffusionProgress> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get list of available generation models.
        /// </summary>
        IReadOnlyList<ModelInfo> GetAvailableModels();

        /// <summary>
        /// Get list of available upscale models.
        /// </summary>
        IReadOnlyList<ModelInfo> GetAvailableUpscaleModels();

        /// <summary>
        /// Check if a model is currently loaded in cache.
        /// </summary>
        bool IsModelLoaded(string modelName);

        /// <summary>
        /// Preload a model into cache.
        /// </summary>
        Task PreloadModelAsync(string modelName, CancellationToken cancellationToken = default);
    }
}
