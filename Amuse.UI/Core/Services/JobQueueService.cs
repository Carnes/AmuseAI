using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amuse.UI.Core.Models;
using Microsoft.Extensions.Logging;

namespace Amuse.UI.Core.Services
{
    /// <summary>
    /// Central job queue service for all frontends.
    /// Processes generation jobs sequentially from a thread-safe queue.
    /// </summary>
    public class JobQueueService : IJobQueueService, IDisposable
    {
        private readonly ILogger<JobQueueService> _logger;
        private readonly IGenerationService _generationService;
        private readonly BlockingCollection<GenerationJob> _jobQueue;
        private readonly ConcurrentDictionary<Guid, GenerationJob> _allJobs;
        private readonly CancellationTokenSource _processingCts;
        private readonly Task _processingTask;
        private GenerationJob _currentJob;

        public JobQueueService(IGenerationService generationService, ILogger<JobQueueService> logger)
        {
            _logger = logger;
            _generationService = generationService;
            _jobQueue = new BlockingCollection<GenerationJob>();
            _allJobs = new ConcurrentDictionary<Guid, GenerationJob>();
            _processingCts = new CancellationTokenSource();

            // Start background processing task
            _processingTask = Task.Factory.StartNew(
                () => ProcessQueueAsync(_processingCts.Token),
                TaskCreationOptions.LongRunning).Unwrap();
        }

        public int QueueLength => _jobQueue.Count;

        public bool IsProcessing => _currentJob != null;

        public event EventHandler<JobStatusChangedEventArgs> JobStatusChanged;
        public event EventHandler<JobProgressEventArgs> JobProgressChanged;

        public Task<Guid> EnqueueAsync(GenerationJob job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            _allJobs[job.Id] = job;
            _jobQueue.Add(job);

            _logger.LogInformation("[JobQueue] Enqueued job {JobId} of type {JobType} from {Source}",
                job.Id, job.Type, job.Source);

            RaiseJobStatusChanged(job.Id, JobStatus.Pending, JobStatus.Pending, job.Source);

            return Task.FromResult(job.Id);
        }

        public GenerationJob GetJob(Guid jobId)
        {
            _allJobs.TryGetValue(jobId, out var job);
            return job;
        }

        public IReadOnlyList<GenerationJob> GetJobs(string source = null)
        {
            var jobs = _allJobs.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(source))
                jobs = jobs.Where(j => j.Source == source);

            return jobs.OrderByDescending(j => j.CreatedAt).ToList();
        }

        public IReadOnlyList<GenerationJob> GetActiveJobs()
        {
            return _allJobs.Values
                .Where(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Processing)
                .OrderBy(j => j.CreatedAt)
                .ToList();
        }

        public int GetQueuePosition(Guid jobId)
        {
            var job = GetJob(jobId);
            if (job == null || job.Status == JobStatus.Completed ||
                job.Status == JobStatus.Failed || job.Status == JobStatus.Cancelled)
                return -1;

            if (job.Status == JobStatus.Processing)
                return 0;

            var activeJobs = GetActiveJobs();
            var index = activeJobs.ToList().FindIndex(j => j.Id == jobId);
            return index >= 0 ? index : -1;
        }

        public async Task<GenerationJobResult> WaitForCompletionAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            var job = GetJob(jobId);
            if (job == null)
                throw new ArgumentException($"Job {jobId} not found", nameof(jobId));

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, job.CancellationTokenSource?.Token ?? CancellationToken.None);

            try
            {
                return await job.CompletionSource.Task.WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        public bool TryCancel(Guid jobId)
        {
            var job = GetJob(jobId);
            if (job == null)
                return false;

            if (job.Status == JobStatus.Completed || job.Status == JobStatus.Failed ||
                job.Status == JobStatus.Cancelled)
                return false;

            var oldStatus = job.Status;
            job.CancellationTokenSource?.Cancel();
            job.Status = JobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            job.CompletionSource.TrySetCanceled();

            _logger.LogInformation("[JobQueue] Cancelled job {JobId}", jobId);
            RaiseJobStatusChanged(jobId, oldStatus, JobStatus.Cancelled, job.Source);

            return true;
        }

        public void ClearCompletedJobs(TimeSpan? olderThan = null)
        {
            var toRemove = _allJobs.Values
                .Where(j => j.Status == JobStatus.Completed || j.Status == JobStatus.Failed ||
                            j.Status == JobStatus.Cancelled)
                .Where(j => !olderThan.HasValue ||
                           (j.CompletedAt.HasValue && j.CompletedAt.Value < DateTime.UtcNow - olderThan.Value))
                .Select(j => j.Id)
                .ToList();

            foreach (var id in toRemove)
            {
                _allJobs.TryRemove(id, out _);
            }

            _logger.LogInformation("[JobQueue] Cleared {Count} completed jobs", toRemove.Count);
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[JobQueue] Started job processing loop");

            try
            {
                foreach (var job in _jobQueue.GetConsumingEnumerable(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (job.Status == JobStatus.Cancelled)
                        continue;

                    await ProcessJobAsync(job, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("[JobQueue] Job processing loop cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JobQueue] Fatal error in job processing loop");
            }
        }

        private async Task ProcessJobAsync(GenerationJob job, CancellationToken cancellationToken)
        {
            _currentJob = job;
            var oldStatus = job.Status;
            job.Status = JobStatus.Processing;
            job.StartedAt = DateTime.UtcNow;

            _logger.LogInformation("[JobQueue] Processing job {JobId} of type {JobType}", job.Id, job.Type);
            RaiseJobStatusChanged(job.Id, oldStatus, JobStatus.Processing, job.Source);

            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, job.CancellationTokenSource?.Token ?? CancellationToken.None);

                var progress = new Progress<OnnxStack.StableDiffusion.Common.DiffusionProgress>(p =>
                {
                    job.Progress = (int)((float)p.StepValue / p.StepMax * 100);
                    job.ProgressMessage = $"Step {p.StepValue}/{p.StepMax}";
                    RaiseJobProgressChanged(job.Id, job.Progress, job.ProgressMessage, job.Source);
                });

                GenerationJobResult result = job.Type switch
                {
                    JobType.TextToImage => await ProcessTextToImageAsync(job, progress, linkedCts.Token),
                    JobType.ImageToImage => await ProcessImageToImageAsync(job, progress, linkedCts.Token),
                    JobType.Upscale => await ProcessUpscaleAsync(job, progress, linkedCts.Token),
                    _ => throw new NotSupportedException($"Job type {job.Type} is not supported")
                };

                job.Status = JobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                job.Progress = 100;
                job.ProgressMessage = "Completed";
                job.CompletionSource.TrySetResult(result);

                _logger.LogInformation("[JobQueue] Completed job {JobId} in {Elapsed:F2}s",
                    job.Id, result.ElapsedSeconds);
                RaiseJobStatusChanged(job.Id, JobStatus.Processing, JobStatus.Completed, job.Source);
            }
            catch (OperationCanceledException)
            {
                job.Status = JobStatus.Cancelled;
                job.CompletedAt = DateTime.UtcNow;
                job.ProgressMessage = "Cancelled";
                job.CompletionSource.TrySetCanceled();

                _logger.LogInformation("[JobQueue] Job {JobId} was cancelled", job.Id);
                RaiseJobStatusChanged(job.Id, JobStatus.Processing, JobStatus.Cancelled, job.Source);
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
                job.ProgressMessage = "Failed";
                job.CompletionSource.TrySetException(ex);

                _logger.LogError(ex, "[JobQueue] Job {JobId} failed", job.Id);
                RaiseJobStatusChanged(job.Id, JobStatus.Processing, JobStatus.Failed, job.Source);
            }
            finally
            {
                _currentJob = null;
            }
        }

        private async Task<GenerationJobResult> ProcessTextToImageAsync(
            GenerationJob job,
            IProgress<OnnxStack.StableDiffusion.Common.DiffusionProgress> progress,
            CancellationToken cancellationToken)
        {
            var parameters = job.RequestData as TextToImageParameters
                ?? throw new InvalidOperationException("Invalid request data for TextToImage job");

            return await _generationService.GenerateTextToImageAsync(parameters, progress, cancellationToken);
        }

        private async Task<GenerationJobResult> ProcessImageToImageAsync(
            GenerationJob job,
            IProgress<OnnxStack.StableDiffusion.Common.DiffusionProgress> progress,
            CancellationToken cancellationToken)
        {
            var parameters = job.RequestData as ImageToImageParameters
                ?? throw new InvalidOperationException("Invalid request data for ImageToImage job");

            return await _generationService.GenerateImageToImageAsync(parameters, progress, cancellationToken);
        }

        private async Task<GenerationJobResult> ProcessUpscaleAsync(
            GenerationJob job,
            IProgress<OnnxStack.StableDiffusion.Common.DiffusionProgress> progress,
            CancellationToken cancellationToken)
        {
            var parameters = job.RequestData as UpscaleParameters
                ?? throw new InvalidOperationException("Invalid request data for Upscale job");

            return await _generationService.UpscaleImageAsync(parameters, progress, cancellationToken);
        }

        private void RaiseJobStatusChanged(Guid jobId, JobStatus oldStatus, JobStatus newStatus, string source)
        {
            JobStatusChanged?.Invoke(this, new JobStatusChangedEventArgs
            {
                JobId = jobId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Source = source
            });
        }

        private void RaiseJobProgressChanged(Guid jobId, int progress, string message, string source)
        {
            JobProgressChanged?.Invoke(this, new JobProgressEventArgs
            {
                JobId = jobId,
                Progress = progress,
                Message = message,
                Source = source
            });
        }

        public void Dispose()
        {
            _processingCts.Cancel();
            _jobQueue.CompleteAdding();

            try
            {
                _processingTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch { }

            _processingCts.Dispose();
            _jobQueue.Dispose();
        }
    }
}
