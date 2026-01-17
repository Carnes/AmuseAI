using System;
using Amuse.UI.Core.Models;

namespace Amuse.UI.Core.Services
{
    /// <summary>
    /// Event args for job status changes.
    /// </summary>
    public class JobStatusChangedEventArgs : EventArgs
    {
        public Guid JobId { get; init; }
        public JobStatus OldStatus { get; init; }
        public JobStatus NewStatus { get; init; }
        public string Source { get; init; }
    }

    /// <summary>
    /// Event args for job progress updates.
    /// </summary>
    public class JobProgressEventArgs : EventArgs
    {
        public Guid JobId { get; init; }
        public int Progress { get; init; }
        public string Message { get; init; }
        public string Source { get; init; }
    }

    /// <summary>
    /// Event args for successful job completion with result.
    /// Used for UI history integration.
    /// </summary>
    public class JobCompletedEventArgs : EventArgs
    {
        public Guid JobId { get; init; }
        public GenerationJob Job { get; init; }
        public GenerationJobResult Result { get; init; }
        public string Source { get; init; }
        public JobType JobType { get; init; }
    }
}
