using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amuse.UI.Core.Models;
using Amuse.UI.Core.Services;
using Amuse.UI.Frontends.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Amuse.UI.Frontends.Api.Controllers
{
    /// <summary>
    /// API controller for job management endpoints.
    /// </summary>
    [ApiController]
    [Route("api/jobs")]
    public class JobsController : ControllerBase
    {
        private readonly IJobQueueService _jobQueueService;
        private readonly ILogger<JobsController> _logger;

        public JobsController(IJobQueueService jobQueueService, ILogger<JobsController> logger)
        {
            _jobQueueService = jobQueueService;
            _logger = logger;
        }

        /// <summary>
        /// Get status of a specific job.
        /// </summary>
        /// <param name="jobId">The job ID.</param>
        /// <returns>Job status.</returns>
        [HttpGet("{jobId}")]
        [ProducesResponseType(typeof(JobResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        public IActionResult GetJob(Guid jobId)
        {
            var job = _jobQueueService.GetJob(jobId);
            if (job == null)
                return NotFound(ErrorResponse.NotFound($"Job {jobId} not found"));

            return Ok(MapToJobResponse(job));
        }

        /// <summary>
        /// Get the result of a completed job.
        /// Returns the generated image as PNG.
        /// </summary>
        /// <param name="jobId">The job ID.</param>
        /// <returns>The generated image.</returns>
        [HttpGet("{jobId}/result")]
        [ProducesResponseType(typeof(byte[]), 200, "image/png")]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        public async Task<IActionResult> GetJobResult(Guid jobId)
        {
            var job = _jobQueueService.GetJob(jobId);
            if (job == null)
                return NotFound(ErrorResponse.NotFound($"Job {jobId} not found"));

            if (job.Status != JobStatus.Completed)
                return BadRequest(ErrorResponse.JobNotComplete(job.Status.ToString().ToLowerInvariant()));

            try
            {
                var result = await job.CompletionSource.Task;
                return File(result.ImageData, result.ImageFormat ?? "image/png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API] Error retrieving result for job {JobId}", jobId);
                return StatusCode(500, ErrorResponse.InternalError("Failed to retrieve job result"));
            }
        }

        /// <summary>
        /// Get metadata of a completed job result.
        /// </summary>
        /// <param name="jobId">The job ID.</param>
        /// <returns>Result metadata.</returns>
        [HttpGet("{jobId}/result/metadata")]
        [ProducesResponseType(typeof(JobResultResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        public async Task<IActionResult> GetJobResultMetadata(Guid jobId)
        {
            var job = _jobQueueService.GetJob(jobId);
            if (job == null)
                return NotFound(ErrorResponse.NotFound($"Job {jobId} not found"));

            if (job.Status != JobStatus.Completed)
                return BadRequest(ErrorResponse.JobNotComplete(job.Status.ToString().ToLowerInvariant()));

            try
            {
                var result = await job.CompletionSource.Task;
                return Ok(new JobResultResponse
                {
                    JobId = jobId,
                    ModelName = result.ModelName,
                    Width = result.Width,
                    Height = result.Height,
                    Seed = result.Seed,
                    InferenceSteps = result.InferenceSteps,
                    GuidanceScale = result.GuidanceScale,
                    ElapsedSeconds = result.ElapsedSeconds,
                    ContentType = result.ImageFormat ?? "image/png"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API] Error retrieving result metadata for job {JobId}", jobId);
                return StatusCode(500, ErrorResponse.InternalError("Failed to retrieve job result metadata"));
            }
        }

        /// <summary>
        /// List all jobs, optionally filtered by source.
        /// </summary>
        /// <param name="source">Optional source filter (e.g., "API", "UI").</param>
        /// <returns>List of jobs.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(JobListResponse), 200)]
        public IActionResult ListJobs([FromQuery] string source = null)
        {
            var jobs = _jobQueueService.GetJobs(source);

            return Ok(new JobListResponse
            {
                Jobs = jobs.Select(MapToJobResponse).ToArray(),
                QueueLength = _jobQueueService.QueueLength,
                IsProcessing = _jobQueueService.IsProcessing
            });
        }

        /// <summary>
        /// Cancel a pending or processing job.
        /// </summary>
        /// <param name="jobId">The job ID.</param>
        /// <returns>Updated job status.</returns>
        [HttpPost("{jobId}/cancel")]
        [ProducesResponseType(typeof(JobResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public IActionResult CancelJob(Guid jobId)
        {
            var job = _jobQueueService.GetJob(jobId);
            if (job == null)
                return NotFound(ErrorResponse.NotFound($"Job {jobId} not found"));

            if (!_jobQueueService.TryCancel(jobId))
                return BadRequest(ErrorResponse.BadRequest("Cannot cancel job", $"Job status: {job.Status}"));

            _logger.LogInformation("[API] Cancelled job {JobId}", jobId);

            return Ok(MapToJobResponse(_jobQueueService.GetJob(jobId)));
        }

        /// <summary>
        /// Wait for a job to complete (long-polling).
        /// </summary>
        /// <param name="jobId">The job ID.</param>
        /// <param name="timeoutSeconds">Maximum wait time in seconds (default: 300).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Completed job status.</returns>
        [HttpGet("{jobId}/wait")]
        [ProducesResponseType(typeof(JobResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesResponseType(typeof(ErrorResponse), 408)]
        public async Task<IActionResult> WaitForJob(
            Guid jobId,
            [FromQuery] int timeoutSeconds = 300,
            CancellationToken cancellationToken = default)
        {
            var job = _jobQueueService.GetJob(jobId);
            if (job == null)
                return NotFound(ErrorResponse.NotFound($"Job {jobId} not found"));

            if (job.Status == JobStatus.Completed || job.Status == JobStatus.Failed ||
                job.Status == JobStatus.Cancelled)
            {
                return Ok(MapToJobResponse(job));
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await _jobQueueService.WaitForCompletionAsync(jobId, linkedCts.Token);
                return Ok(MapToJobResponse(_jobQueueService.GetJob(jobId)));
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                return StatusCode(408, ErrorResponse.BadRequest("Request timeout", "Job did not complete within timeout"));
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, ErrorResponse.BadRequest("Client closed request"));
            }
        }

        private JobResponse MapToJobResponse(GenerationJob job)
        {
            return new JobResponse
            {
                JobId = job.Id,
                Status = job.Status.ToString().ToLowerInvariant(),
                Type = job.Type.ToString(),
                Source = job.Source,
                Progress = job.Progress,
                ProgressMessage = job.ProgressMessage,
                QueuePosition = _jobQueueService.GetQueuePosition(job.Id),
                CreatedAt = job.CreatedAt,
                StartedAt = job.StartedAt,
                CompletedAt = job.CompletedAt,
                ErrorMessage = job.ErrorMessage
            };
        }
    }
}
