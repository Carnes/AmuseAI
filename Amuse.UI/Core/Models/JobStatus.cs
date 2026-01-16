namespace Amuse.UI.Core.Models
{
    /// <summary>
    /// Status of a generation job in the queue.
    /// </summary>
    public enum JobStatus
    {
        /// <summary>Job is queued and waiting to be processed.</summary>
        Pending,

        /// <summary>Job is currently being processed.</summary>
        Processing,

        /// <summary>Job completed successfully.</summary>
        Completed,

        /// <summary>Job failed with an error.</summary>
        Failed,

        /// <summary>Job was cancelled by user.</summary>
        Cancelled
    }

    /// <summary>
    /// Type of generation job.
    /// </summary>
    public enum JobType
    {
        TextToImage,
        ImageToImage,
        Inpaint,
        Upscale,
        TextToVideo,
        ImageToVideo,
        VideoToVideo,
        FeatureExtraction
    }
}
