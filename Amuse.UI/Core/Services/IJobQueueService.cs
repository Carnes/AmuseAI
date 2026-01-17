using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amuse.UI.Core.Models;

namespace Amuse.UI.Core.Services
{
    /// <summary>
    /// Central job queue service for all frontends (UI, API, Discord, etc.).
    /// Manages queuing, processing, and status tracking of generation jobs.
    /// </summary>
    public interface IJobQueueService
    {
        /// <summary>
        /// Enqueue a new generation job.
        /// </summary>
        /// <param name="job">The job to enqueue.</param>
        /// <returns>The job ID.</returns>
        Task<Guid> EnqueueAsync(GenerationJob job);

        /// <summary>
        /// Get a job by its ID.
        /// </summary>
        /// <param name="jobId">The job ID.</param>
        /// <returns>The job, or null if not found.</returns>
        GenerationJob GetJob(Guid jobId);

        /// <summary>
        /// Get all jobs, optionally filtered by source.
        /// </summary>
        /// <param name="source">Optional source filter (e.g., "API", "UI").</param>
        /// <returns>List of jobs matching the filter.</returns>
        IReadOnlyList<GenerationJob> GetJobs(string source = null);

        /// <summary>
        /// Get all currently active (pending or processing) jobs.
        /// </summary>
        /// <returns>List of active jobs.</returns>
        IReadOnlyList<GenerationJob> GetActiveJobs();

        /// <summary>
        /// Get the current queue position for a job.
        /// </summary>
        /// <param name="jobId">The job ID.</param>
        /// <returns>Queue position (0 = processing, 1 = next, etc.), or -1 if not found/completed.</returns>
        int GetQueuePosition(Guid jobId);

        /// <summary>
        /// Wait for a job to complete.
        /// </summary>
        /// <param name="jobId">The job ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The job result when completed.</returns>
        Task<GenerationJobResult> WaitForCompletionAsync(Guid jobId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempt to cancel a job.
        /// </summary>
        /// <param name="jobId">The job ID.</param>
        /// <returns>True if cancellation was initiated, false if job not found or already completed.</returns>
        bool TryCancel(Guid jobId);

        /// <summary>
        /// Clear completed/failed/cancelled jobs from history.
        /// </summary>
        /// <param name="olderThan">Optional: only clear jobs older than this.</param>
        void ClearCompletedJobs(TimeSpan? olderThan = null);

        /// <summary>
        /// Current number of jobs in queue (pending).
        /// </summary>
        int QueueLength { get; }

        /// <summary>
        /// Whether a job is currently being processed.
        /// </summary>
        bool IsProcessing { get; }

        /// <summary>
        /// Event raised when a job's status changes.
        /// </summary>
        event EventHandler<JobStatusChangedEventArgs> JobStatusChanged;

        /// <summary>
        /// Event raised when a job's progress updates.
        /// </summary>
        event EventHandler<JobProgressEventArgs> JobProgressChanged;

        /// <summary>
        /// Event raised when a job completes successfully with its result.
        /// Used for UI history integration.
        /// </summary>
        event EventHandler<JobCompletedEventArgs> JobCompleted;

        /// <summary>
        /// Acquires the generation lock. Only one generation can run at a time.
        /// Both UI and API should acquire this before generating.
        /// The returned IDisposable releases the lock when disposed.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A disposable that releases the lock when disposed.</returns>
        Task<IDisposable> AcquireGenerationLockAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns true if the generation lock is currently held.
        /// </summary>
        bool IsGenerationLockHeld { get; }
    }
}
