using Amuse.UI.Enums;
using Amuse.UI.Models;
using Microsoft.ML.OnnxRuntime;
using OnnxStack.Core.Model;
using OnnxStack.StableDiffusion.Enums;
using System;
using System.IO;
using System.Linq;

namespace Amuse.UI.Services
{
    public class ProviderService : IProviderService
    {
        private readonly AmuseSettings _settings;

        public ProviderService(AmuseSettings settings)
        {
            _settings = settings;
        }


        public OnnxExecutionProvider GetProvider(ExecutionProvider? provider, int? deviceId)
        {
            var selectedDevice = deviceId ?? _settings.DefaultExecutionDevice.DeviceId;
            var selectedProvider = provider ?? _settings.DefaultExecutionDevice.Provider;
            var cacheDirectory = Path.Combine(App.CacheDirectory, selectedProvider.ToString());
            return selectedProvider switch
            {
                ExecutionProvider.DirectML => DirectML(selectedDevice),
                _ => CPU()
            };
        }


        private static OnnxExecutionProvider CPU()
        {
            return new OnnxExecutionProvider(nameof(ExecutionProvider.CPU), configuration =>
            {
                var sessionOptions = new SessionOptions
                {
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL
                };

                sessionOptions.AppendExecutionProvider_CPU();
                return sessionOptions;
            });
        }


        private static OnnxExecutionProvider DirectML(int deviceId)
        {
            return new OnnxExecutionProvider(nameof(ExecutionProvider.DirectML), configuration =>
            {
                var sessionOptions = new SessionOptions
                {
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL
                };

                sessionOptions.AppendExecutionProvider_DML(deviceId);
                sessionOptions.AppendExecutionProvider_CPU();
                return sessionOptions;
            });
        }

    }
}
