using System;
using System.Threading;
using System.Threading.Tasks;
using Amuse.UI.Core.Services;
using Amuse.UI.Frontends.Api.Controllers;
using Amuse.UI.Models;
using Amuse.UI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Amuse.UI.Frontends.Api
{
    /// <summary>
    /// Hosted service that runs the ASP.NET Core API server.
    /// Runs alongside the WPF application without blocking.
    /// </summary>
    public class ApiHostService : IHostedService, IDisposable
    {
        private readonly ILogger<ApiHostService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly AmuseSettings _settings;
        private WebApplication _webApp;
        private Task _runTask;
        private CancellationTokenSource _cts;

        public ApiHostService(
            IServiceProvider serviceProvider,
            AmuseSettings settings,
            ILogger<ApiHostService> logger)
        {
            _serviceProvider = serviceProvider;
            _settings = settings;
            _logger = logger;
        }

        /// <summary>
        /// Gets whether the API server is currently running.
        /// </summary>
        public bool IsRunning => _webApp != null && _runTask != null && !_runTask.IsCompleted;

        /// <summary>
        /// Gets the URL the API is listening on.
        /// </summary>
        public string ListeningUrl { get; private set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[ApiHostService] Starting API server...");

            try
            {
                _cts = new CancellationTokenSource();

                var builder = WebApplication.CreateBuilder();

                // Configure Kestrel to listen on specified port
                var port = _settings.ApiPort > 0 ? _settings.ApiPort : 5000;
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenLocalhost(port);
                });

                // Add controllers from this assembly
                builder.Services.AddControllers()
                    .AddApplicationPart(typeof(GenerateController).Assembly);

                // Add Swagger for API documentation
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                    {
                        Title = "AmuseAI API",
                        Version = "v1",
                        Description = "REST API for AmuseAI image generation"
                    });
                });

                // Register services from the main application
                builder.Services.AddSingleton(_serviceProvider.GetRequiredService<IJobQueueService>());
                builder.Services.AddSingleton(_serviceProvider.GetRequiredService<IGenerationService>());
                builder.Services.AddSingleton(_serviceProvider.GetRequiredService<IModelCacheService>());
                builder.Services.AddSingleton(_settings);

                // Configure logging
                builder.Logging.ClearProviders();
                builder.Logging.AddConsole();

                _webApp = builder.Build();

                // Configure middleware pipeline
                _webApp.UseSwagger();
                _webApp.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AmuseAI API v1");
                    c.RoutePrefix = "swagger";
                });

                _webApp.MapControllers();

                ListeningUrl = $"http://localhost:{port}";

                // Run non-blocking
                _runTask = _webApp.RunAsync(_cts.Token);

                _logger.LogInformation("[ApiHostService] API server started on {Url}", ListeningUrl);
                _logger.LogInformation("[ApiHostService] Swagger UI available at {Url}/swagger", ListeningUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ApiHostService] Failed to start API server");
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[ApiHostService] Stopping API server...");

            try
            {
                _cts?.Cancel();

                if (_webApp != null)
                {
                    await _webApp.StopAsync(cancellationToken);
                }

                if (_runTask != null)
                {
                    try
                    {
                        await _runTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("[ApiHostService] API server did not stop within timeout");
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                }

                _logger.LogInformation("[ApiHostService] API server stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ApiHostService] Error stopping API server");
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _webApp?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
        }
    }
}
