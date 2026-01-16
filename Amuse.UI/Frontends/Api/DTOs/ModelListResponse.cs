using System.Collections.Generic;

namespace Amuse.UI.Frontends.Api.DTOs
{
    /// <summary>
    /// Response DTO for listing available models.
    /// </summary>
    public class ModelListResponse
    {
        /// <summary>
        /// List of available generation models.
        /// </summary>
        public List<ModelInfoResponse> Models { get; set; } = new();

        /// <summary>
        /// List of available upscale models.
        /// </summary>
        public List<ModelInfoResponse> UpscaleModels { get; set; } = new();
    }

    /// <summary>
    /// Response DTO for model information.
    /// </summary>
    public class ModelInfoResponse
    {
        /// <summary>
        /// Model name (used in requests).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Pipeline type (e.g., "StableDiffusion", "StableDiffusionXL", "Flux").
        /// </summary>
        public string PipelineType { get; set; }

        /// <summary>
        /// Default image width for this model.
        /// </summary>
        public int DefaultWidth { get; set; }

        /// <summary>
        /// Default image height for this model.
        /// </summary>
        public int DefaultHeight { get; set; }

        /// <summary>
        /// List of supported scheduler types.
        /// </summary>
        public List<string> SupportedSchedulers { get; set; } = new();

        /// <summary>
        /// List of supported diffuser types (e.g., "TextToImage", "ImageToImage").
        /// </summary>
        public List<string> SupportedDiffusers { get; set; } = new();

        /// <summary>
        /// Whether the model is currently loaded in memory.
        /// </summary>
        public bool IsLoaded { get; set; }
    }

    /// <summary>
    /// Response DTO for health check.
    /// </summary>
    public class HealthResponse
    {
        /// <summary>
        /// Service status: "healthy" or "unhealthy".
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Application version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Number of jobs currently in queue.
        /// </summary>
        public int QueueLength { get; set; }

        /// <summary>
        /// Whether a job is currently being processed.
        /// </summary>
        public bool IsProcessing { get; set; }

        /// <summary>
        /// Number of loaded models.
        /// </summary>
        public int LoadedModelsCount { get; set; }

        /// <summary>
        /// Server uptime in seconds.
        /// </summary>
        public double UptimeSeconds { get; set; }
    }
}
