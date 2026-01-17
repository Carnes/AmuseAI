using System;
using System.ComponentModel.DataAnnotations;

namespace Amuse.UI.Frontends.Api.DTOs
{
    /// <summary>
    /// Request DTO for text-to-image generation.
    /// </summary>
    public class TextToImageRequest
    {
        /// <summary>
        /// Name of the model to use for generation.
        /// If not specified, uses the default model from settings.
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// The text prompt describing the desired image.
        /// </summary>
        [Required]
        [StringLength(512, MinimumLength = 1)]
        public string Prompt { get; set; }

        /// <summary>
        /// Optional negative prompt describing what to avoid.
        /// </summary>
        [StringLength(512)]
        public string NegativePrompt { get; set; }

        /// <summary>
        /// Image width in pixels (default: model default, typically 512 or 1024).
        /// </summary>
        [Range(64, 4096)]
        public int? Width { get; set; }

        /// <summary>
        /// Image height in pixels (default: model default, typically 512 or 1024).
        /// </summary>
        [Range(64, 4096)]
        public int? Height { get; set; }

        /// <summary>
        /// Random seed for reproducibility. 0 = random seed.
        /// </summary>
        [Range(0, int.MaxValue)]
        public int? Seed { get; set; } = 0;

        /// <summary>
        /// Number of inference steps (default: model default, typically 20-30).
        /// </summary>
        [Range(1, 200)]
        public int? Steps { get; set; }

        /// <summary>
        /// Guidance scale for classifier-free guidance (default: model default).
        /// Higher values follow prompt more closely.
        /// </summary>
        [Range(0, 30)]
        public float? GuidanceScale { get; set; }

        /// <summary>
        /// Scheduler type (e.g., "EulerAncestral", "DDPM", "LCM").
        /// If not specified, uses model default.
        /// </summary>
        public string SchedulerType { get; set; }
    }

    /// <summary>
    /// Request DTO for image upscaling.
    /// </summary>
    public class UpscaleRequest
    {
        /// <summary>
        /// Name of the upscale model to use.
        /// If not specified, uses the default upscale model from settings.
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// Base64-encoded input image to upscale.
        /// Either this or SourceJobId must be provided.
        /// </summary>
        public string InputImageBase64 { get; set; }

        /// <summary>
        /// Job ID of a completed generation job to use as input.
        /// Either this or InputImageBase64 must be provided.
        /// </summary>
        public Guid? SourceJobId { get; set; }

        /// <summary>
        /// Scale factor (e.g., 2 for 2x upscale).
        /// </summary>
        [Range(1, 8)]
        public int? ScaleFactor { get; set; } = 2;
    }
}
