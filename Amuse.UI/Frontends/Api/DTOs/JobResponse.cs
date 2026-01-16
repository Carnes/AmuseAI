using System;

namespace Amuse.UI.Frontends.Api.DTOs
{
    /// <summary>
    /// Response DTO for job status.
    /// </summary>
    public class JobResponse
    {
        /// <summary>
        /// Unique identifier for the job.
        /// </summary>
        public Guid JobId { get; set; }

        /// <summary>
        /// Current status of the job: pending, processing, completed, failed, cancelled.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Type of job: TextToImage, ImageToImage, Upscale, etc.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Source that created the job (e.g., "API", "UI").
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Current progress percentage (0-100).
        /// </summary>
        public int Progress { get; set; }

        /// <summary>
        /// Human-readable progress message.
        /// </summary>
        public string ProgressMessage { get; set; }

        /// <summary>
        /// Position in the queue (0 = currently processing, 1+ = waiting).
        /// -1 if completed/failed/cancelled.
        /// </summary>
        public int QueuePosition { get; set; }

        /// <summary>
        /// When the job was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the job started processing (null if still pending).
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When the job completed (null if still processing).
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Error message if the job failed.
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response DTO for job result metadata.
    /// </summary>
    public class JobResultResponse
    {
        /// <summary>
        /// Job ID.
        /// </summary>
        public Guid JobId { get; set; }

        /// <summary>
        /// Model used for generation.
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// Image width in pixels.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Image height in pixels.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Seed used for generation.
        /// </summary>
        public int Seed { get; set; }

        /// <summary>
        /// Number of inference steps used.
        /// </summary>
        public int InferenceSteps { get; set; }

        /// <summary>
        /// Guidance scale used.
        /// </summary>
        public float GuidanceScale { get; set; }

        /// <summary>
        /// Generation time in seconds.
        /// </summary>
        public double ElapsedSeconds { get; set; }

        /// <summary>
        /// MIME type of the result image.
        /// </summary>
        public string ContentType { get; set; }
    }

    /// <summary>
    /// Response DTO for listing jobs.
    /// </summary>
    public class JobListResponse
    {
        /// <summary>
        /// List of jobs.
        /// </summary>
        public JobResponse[] Jobs { get; set; }

        /// <summary>
        /// Total number of jobs in queue.
        /// </summary>
        public int QueueLength { get; set; }

        /// <summary>
        /// Whether a job is currently being processed.
        /// </summary>
        public bool IsProcessing { get; set; }
    }
}
