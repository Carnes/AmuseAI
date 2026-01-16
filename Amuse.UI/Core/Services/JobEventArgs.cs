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
}
