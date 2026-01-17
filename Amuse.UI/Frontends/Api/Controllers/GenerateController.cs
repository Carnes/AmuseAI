using System;
using System.Linq;
using System.Threading.Tasks;
using Amuse.UI.Core.Models;
using Amuse.UI.Core.Services;
using Amuse.UI.Frontends.Api.DTOs;
using Amuse.UI.Models;
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
        private readonly IGenerationService _generationService;
        private readonly AmuseSettings _settings;
        private readonly ILogger<GenerateController> _logger;

        public GenerateController(
            IJobQueueService jobQueueService,
            IGenerationService generationService,
            AmuseSettings settings,
            ILogger<GenerateController> logger)
        {
            _jobQueueService = jobQueueService;
            _generationService = generationService;
            _settings = settings;
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
                return BadRequestWithWarning("Invalid request", ModelState.ToString());

            var defaults = _settings?.GenerationDefaults ?? new GenerationDefaultSettings();

            // Apply defaults for missing parameters
            var modelName = !string.IsNullOrEmpty(request.ModelName)
                ? request.ModelName
                : defaults.DefaultModelName;

            // If no model specified and no default, try to use first available model
            if (string.IsNullOrEmpty(modelName))
            {
                var availableModels = _generationService.GetAvailableModels();
                modelName = availableModels.FirstOrDefault()?.Name;
            }

            if (string.IsNullOrEmpty(modelName))
                return BadRequestWithWarning("ModelName is required and no default model is configured");

            var negativePrompt = request.NegativePrompt ?? defaults.DefaultNegativePrompt ?? string.Empty;
            var steps = request.Steps ?? (defaults.DefaultSteps > 0 ? defaults.DefaultSteps : 30);
            var guidanceScale = request.GuidanceScale ?? (defaults.DefaultGuidanceScale > 0 ? defaults.DefaultGuidanceScale : 7.5f);
            var schedulerType = !string.IsNullOrEmpty(request.SchedulerType)
                ? request.SchedulerType
                : defaults.DefaultSchedulerType;

            // Get model info to determine default dimensions
            var modelInfo = _generationService.GetAvailableModels().FirstOrDefault(m => m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));
            var defaultWidth = modelInfo?.DefaultWidth ?? (defaults.DefaultWidth > 0 ? defaults.DefaultWidth : 512);
            var defaultHeight = modelInfo?.DefaultHeight ?? (defaults.DefaultHeight > 0 ? defaults.DefaultHeight : 512);

            var width = request.Width ?? defaultWidth;
            var height = request.Height ?? defaultHeight;

            // Validate dimensions for the selected model
            var dimensionValidation = _generationService.ValidateDimensions(modelName, width, height);
            if (!dimensionValidation.IsValid)
            {
                return BadRequestWithWarning(
                    dimensionValidation.ErrorMessage,
                    $"Valid range: {dimensionValidation.MinDimension}-{dimensionValidation.MaxDimension}, must be divisible by {dimensionValidation.DimensionMultiple}");
            }

            var parameters = new TextToImageParameters
            {
                ModelName = modelName,
                Prompt = request.Prompt,
                NegativePrompt = negativePrompt,
                Width = width,
                Height = height,
                Seed = request.Seed ?? 0,
                Steps = steps,
                GuidanceScale = guidanceScale,
                SchedulerType = schedulerType
            };

            var job = GenerationJob.Create(JobType.TextToImage, "API", parameters);
            await _jobQueueService.EnqueueAsync(job);

            _logger.LogInformation("[API] Queued text-to-image job {JobId} with model {ModelName} at {Width}x{Height}", job.Id, modelName, width, height);

            var response = MapToJobResponse(job);
            return AcceptedAtAction(nameof(JobsController.GetJob), "Jobs", new { jobId = job.Id }, response);
        }

        /// <summary>
        /// Queue an image upscale job.
        /// </summary>
        /// <param name="request">The upscale request with input image or source job ID.</param>
        /// <returns>Job status with ID for tracking.</returns>
        [HttpPost("upscale")]
        [ProducesResponseType(typeof(JobResponse), 202)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public async Task<IActionResult> Upscale([FromBody] UpscaleRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequestWithWarning("Invalid request", ModelState.ToString());

            byte[] inputImage;

            // Get input image from either SourceJobId or InputImageBase64
            if (request.SourceJobId.HasValue)
            {
                var sourceJob = _jobQueueService.GetJob(request.SourceJobId.Value);
                if (sourceJob == null)
                    return BadRequestWithWarning($"Source job {request.SourceJobId} not found");

                if (sourceJob.Status != JobStatus.Completed)
                    return BadRequestWithWarning($"Source job {request.SourceJobId} is not completed (status: {sourceJob.Status})");

                // Wait for the result and get image data
                try
                {
                    var result = await sourceJob.CompletionSource.Task;
                    if (result?.ImageData == null || result.ImageData.Length == 0)
                        return BadRequestWithWarning($"Source job {request.SourceJobId} has no image data");

                    inputImage = result.ImageData;
                    _logger.LogInformation("[API] Using image from job {SourceJobId} for upscale", request.SourceJobId);
                }
                catch (Exception ex)
                {
                    return BadRequestWithWarning($"Failed to get image from source job: {ex.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(request.InputImageBase64))
            {
                try
                {
                    inputImage = Convert.FromBase64String(request.InputImageBase64);
                }
                catch (FormatException)
                {
                    return BadRequestWithWarning("Invalid base64 image data");
                }
            }
            else
            {
                return BadRequestWithWarning("Either SourceJobId or InputImageBase64 must be provided");
            }

            var defaults = _settings?.GenerationDefaults ?? new GenerationDefaultSettings();

            // Apply defaults for model name
            var modelName = !string.IsNullOrEmpty(request.ModelName)
                ? request.ModelName
                : defaults.DefaultUpscaleModelName;

            // If no model specified and no default, try to use first available upscale model
            if (string.IsNullOrEmpty(modelName))
            {
                var availableModels = _generationService.GetAvailableUpscaleModels();
                modelName = availableModels.FirstOrDefault()?.Name;
            }

            if (string.IsNullOrEmpty(modelName))
                return BadRequestWithWarning("ModelName is required and no default upscale model is configured");

            var scaleFactor = request.ScaleFactor ?? (defaults.DefaultScaleFactor > 0 ? defaults.DefaultScaleFactor : 2);

            var parameters = new UpscaleParameters
            {
                ModelName = modelName,
                InputImage = inputImage,
                ScaleFactor = scaleFactor
            };

            var job = GenerationJob.Create(JobType.Upscale, "API", parameters);
            await _jobQueueService.EnqueueAsync(job);

            _logger.LogInformation("[API] Queued upscale job {JobId} with model {ModelName} at {ScaleFactor}x", job.Id, modelName, scaleFactor);

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

        private IActionResult BadRequestWithWarning(string error, string details = null)
        {
            _logger.LogWarning("[API] Bad request: {Error}{Details}", error, details != null ? $" - {details}" : "");
            return BadRequest(ErrorResponse.BadRequest(error, details));
        }
    }
}
