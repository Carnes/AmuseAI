namespace Amuse.UI.Core.Models
{
    /// <summary>
    /// Result of a completed generation job.
    /// Contains the generated image/video data and metadata.
    /// </summary>
    public class GenerationJobResult
    {
        /// <summary>
        /// The generated image data as PNG bytes.
        /// For video jobs, this contains the first frame or thumbnail.
        /// </summary>
        public byte[] ImageData { get; init; }

        /// <summary>
        /// The generated video data as bytes (for video jobs).
        /// Null for image jobs.
        /// </summary>
        public byte[] VideoData { get; init; }

        /// <summary>
        /// MIME type of the image (e.g., "image/png").
        /// </summary>
        public string ImageFormat { get; init; } = "image/png";

        /// <summary>
        /// MIME type of the video if applicable (e.g., "video/mp4").
        /// </summary>
        public string VideoFormat { get; init; }

        /// <summary>
        /// Width of the generated image in pixels.
        /// </summary>
        public int Width { get; init; }

        /// <summary>
        /// Height of the generated image in pixels.
        /// </summary>
        public int Height { get; init; }

        /// <summary>
        /// The actual seed used for generation.
        /// Useful when seed=0 (random) was requested.
        /// </summary>
        public int Seed { get; init; }

        /// <summary>
        /// Total elapsed time for generation in seconds.
        /// </summary>
        public double ElapsedSeconds { get; init; }

        /// <summary>
        /// Name of the model used for generation.
        /// </summary>
        public string ModelName { get; init; }

        /// <summary>
        /// Number of inference steps used.
        /// </summary>
        public int InferenceSteps { get; init; }

        /// <summary>
        /// Guidance scale used.
        /// </summary>
        public float GuidanceScale { get; init; }
    }
}
