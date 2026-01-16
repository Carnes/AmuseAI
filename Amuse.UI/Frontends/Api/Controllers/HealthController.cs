using System;
using System.Diagnostics;
using System.Linq;
using Amuse.UI.Core.Services;
using Amuse.UI.Frontends.Api.DTOs;
using Amuse.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace Amuse.UI.Frontends.Api.Controllers
{
    /// <summary>
    /// API controller for health and status endpoints.
    /// </summary>
    [ApiController]
    [Route("api")]
    public class HealthController : ControllerBase
    {
        private readonly IJobQueueService _jobQueueService;
        private readonly IGenerationService _generationService;
        private readonly AmuseSettings _settings;
        private static readonly DateTime _startTime = DateTime.UtcNow;

        public HealthController(
            IJobQueueService jobQueueService,
            IGenerationService generationService,
            AmuseSettings settings)
        {
            _jobQueueService = jobQueueService;
            _generationService = generationService;
            _settings = settings;
        }

        /// <summary>
        /// Health check endpoint.
        /// </summary>
        /// <returns>Service health status.</returns>
        [HttpGet("health")]
        [ProducesResponseType(typeof(HealthResponse), 200)]
        public IActionResult Health()
        {
            var loadedModels = _generationService.GetAvailableModels()
                .Count(m => m.IsLoaded);

            return Ok(new HealthResponse
            {
                Status = "healthy",
                Version = _settings.FileVersion ?? "unknown",
                QueueLength = _jobQueueService.QueueLength,
                IsProcessing = _jobQueueService.IsProcessing,
                LoadedModelsCount = loadedModels,
                UptimeSeconds = (DateTime.UtcNow - _startTime).TotalSeconds
            });
        }

        /// <summary>
        /// List available models.
        /// </summary>
        /// <returns>List of generation and upscale models.</returns>
        [HttpGet("models")]
        [ProducesResponseType(typeof(ModelListResponse), 200)]
        public IActionResult ListModels()
        {
            var models = _generationService.GetAvailableModels();
            var upscaleModels = _generationService.GetAvailableUpscaleModels();

            return Ok(new ModelListResponse
            {
                Models = models.Select(m => new ModelInfoResponse
                {
                    Name = m.Name,
                    PipelineType = m.PipelineType,
                    DefaultWidth = m.DefaultWidth,
                    DefaultHeight = m.DefaultHeight,
                    SupportedSchedulers = m.SupportedSchedulers?.ToList() ?? new(),
                    SupportedDiffusers = m.SupportedDiffusers?.ToList() ?? new(),
                    IsLoaded = m.IsLoaded
                }).ToList(),
                UpscaleModels = upscaleModels.Select(m => new ModelInfoResponse
                {
                    Name = m.Name,
                    PipelineType = m.PipelineType,
                    DefaultWidth = m.DefaultWidth,
                    DefaultHeight = m.DefaultHeight,
                    SupportedSchedulers = m.SupportedSchedulers?.ToList() ?? new(),
                    SupportedDiffusers = m.SupportedDiffusers?.ToList() ?? new(),
                    IsLoaded = m.IsLoaded
                }).ToList()
            });
        }
    }
}
