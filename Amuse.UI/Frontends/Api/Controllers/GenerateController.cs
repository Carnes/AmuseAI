using System;
using System.Threading.Tasks;
using Amuse.UI.Core.Models;
using Amuse.UI.Core.Services;
using Amuse.UI.Frontends.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Amuse.UI.Frontends.Api.Controllers
{
    /// <summary>
    /// API controller for image generation endpoints.
    /// </summary>
    [ApiController]
    [Route("api/generate")]
    public class GenerateController : ControllerBase
    {
        private readonly IJobQueueService _jobQueueService;
        private readonly ILogger<GenerateController> _logger;

        public GenerateController(IJobQueueService jobQueueService, ILogger<GenerateController> logger)
        {
            _jobQueueService = jobQueueService;
            _logger = logger;
        }

        /// <summary>
        /// Queue a text-to-image generation job.
        /// </summary>
        /// <param name="request">The generation request.</param>
        /// <returns>Job status with ID for tracking.</returns>
        [HttpPost("text-to-image")]
        [ProducesResponseType(typeof(JobResponse), 202)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public async Task<IActionResult> TextToImage([FromBody] TextToImageRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ErrorResponse.BadRequest("Invalid request", ModelState.ToString()));

            var parameters = new TextToImageParameters
            {
                ModelName = request.ModelName,
                Prompt = request.Prompt,
                NegativePrompt = request.NegativePrompt,
                Width = request.Width ?? 0,
                Height = request.Height ?? 0,
                Seed = request.Seed ?? 0,
                Steps = request.Steps ?? 0,
                GuidanceScale = request.GuidanceScale ?? 0,
                SchedulerType = request.SchedulerType
            };

            var job = GenerationJob.Create(JobType.TextToImage, "API", parameters);
            await _jobQueueService.EnqueueAsync(job);

            _logger.LogInformation("[API] Queued text-to-image job {JobId}", job.Id);

            var response = MapToJobResponse(job);
            return AcceptedAtAction(nameof(JobsController.GetJob), "Jobs", new { jobId = job.Id }, response);
        }

        /// <summary>
        /// Queue an image-to-image generation job.
        /// </summary>
        /// <param name="request">The generation request with input image.</param>
        /// <returns>Job status with ID for tracking.</returns>
        [HttpPost("image-to-image")]
        [ProducesResponseType(typeof(JobResponse), 202)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public async Task<IActionResult> ImageToImage([FromBody] ImageToImageRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ErrorResponse.BadRequest("Invalid request", ModelState.ToString()));

            byte[] inputImage;
            try
            {
                inputImage = Convert.FromBase64String(request.InputImageBase64);
            }
            catch (FormatException)
            {
                return BadRequest(ErrorResponse.BadRequest("Invalid base64 image data"));
            }

            var parameters = new ImageToImageParameters
            {
                ModelName = request.ModelName,
                Prompt = request.Prompt,
                NegativePrompt = request.NegativePrompt,
                Width = request.Width ?? 0,
                Height = request.Height ?? 0,
                Seed = request.Seed ?? 0,
                Steps = request.Steps ?? 0,
                GuidanceScale = request.GuidanceScale ?? 0,
                SchedulerType = request.SchedulerType,
                InputImage = inputImage,
                Strength = request.Strength ?? 0.75f
            };

            var job = GenerationJob.Create(JobType.ImageToImage, "API", parameters);
            await _jobQueueService.EnqueueAsync(job);

            _logger.LogInformation("[API] Queued image-to-image job {JobId}", job.Id);

            var response = MapToJobResponse(job);
            return AcceptedAtAction(nameof(JobsController.GetJob), "Jobs", new { jobId = job.Id }, response);
        }

        /// <summary>
        /// Queue an image upscale job.
        /// </summary>
        /// <param name="request">The upscale request with input image.</param>
        /// <returns>Job status with ID for tracking.</returns>
        [HttpPost("upscale")]
        [ProducesResponseType(typeof(JobResponse), 202)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public async Task<IActionResult> Upscale([FromBody] UpscaleRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ErrorResponse.BadRequest("Invalid request", ModelState.ToString()));

            byte[] inputImage;
            try
            {
                inputImage = Convert.FromBase64String(request.InputImageBase64);
            }
            catch (FormatException)
            {
                return BadRequest(ErrorResponse.BadRequest("Invalid base64 image data"));
            }

            var parameters = new UpscaleParameters
            {
                ModelName = request.ModelName,
                InputImage = inputImage,
                ScaleFactor = request.ScaleFactor ?? 2
            };

            var job = GenerationJob.Create(JobType.Upscale, "API", parameters);
            await _jobQueueService.EnqueueAsync(job);

            _logger.LogInformation("[API] Queued upscale job {JobId}", job.Id);

            var response = MapToJobResponse(job);
            return AcceptedAtAction(nameof(JobsController.GetJob), "Jobs", new { jobId = job.Id }, response);
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
